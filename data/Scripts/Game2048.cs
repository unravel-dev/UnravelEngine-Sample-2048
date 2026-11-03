using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unravel.Core;

namespace Sample2048
{
    /// <summary>
    /// The 2048 game. Reads a move (the Move actions, or a mouse swipe), applies it to the Board and plays the
    /// result on pooled tile entities: slides, merges that pop with sparks, a ring, a flash and a light, tiles that
    /// drop in, sounds pitched by value, camera shake, and the HUD's scores and win / game-over panels.
    /// </summary>
    [ScriptSourceFile]
    public class Game2048 : ScriptComponent
    {
        public string tilePrefab = "app:/data/Prefabs/Tile.pfb";
        public Entity tilesRoot;
        public Entity boardRoot;
        public Entity hudEntity;
        public Entity cameraEntity;
        public Entity mergeSparks;
        public Entity mergeRing;
        public Entity mergeFlash;
        public Entity spawnGlint;
        public Entity confetti;
        public Entity mergeLight;

        public float cellSpacing = 1.25f;
        public float tileBaseHeight = 0.03f;
        public float slideSeconds = 0.11f;
        public float popSeconds = 0.22f;
        public float spawnSeconds = 0.24f;
        public int winValue = 2048;
        public float swipePixels = 40.0f;

        public string slideSound = "app:/data/Audio/slide.wav";
        public string mergeSound = "app:/data/Audio/merge.wav";
        public string bigMergeSound = "app:/data/Audio/big_merge.wav";
        public string spawnSound = "app:/data/Audio/spawn.wav";
        public string bumpSound = "app:/data/Audio/bump.wav";
        public string winSound = "app:/data/Audio/win.wav";
        public string gameOverSound = "app:/data/Audio/game_over.wav";
        public string clickSound = "app:/data/Audio/click.wav";
        public string whooshSound = "app:/data/Audio/whoosh.wav";

        private const int BigMergeValue = 128;
        private const float LightFlashDecay = 9.0f;
        private static readonly int[] PentatonicSteps = { 0, 2, 4, 7, 9 };

        private readonly Board board = new Board();
        private readonly Dictionary<Tile, TileView> views = new Dictionary<Tile, TileView>();
        private readonly Dictionary<uint, TileView> viewsByEntity = new Dictionary<uint, TileView>();
        private EntityPool pool;
        private HudModel hud;
        private UIDataModel model;
        private UIDocumentComponent hudDocument;
        private CameraRig cameraRig;
        private ParticleEmitterComponent sparks;
        private ParticleEmitterComponent ring;
        private ParticleEmitterComponent flash;
        private ParticleEmitterComponent glint;
        private ParticleEmitterComponent confettiEmitter;
        private LightComponent flashLight;
        private AudioClip slideClip;
        private AudioClip mergeClip;
        private AudioClip bigMergeClip;
        private AudioClip spawnClip;
        private AudioClip bumpClip;
        private AudioClip winClip;
        private AudioClip gameOverClip;
        private AudioClip clickClip;
        private AudioClip whooshClip;
        private bool busy = true;
        private bool restarting;
        private bool gameOver;
        private bool keepPlaying;
        private Direction? queuedMove;
        private bool swiping;
        private Vector2 swipeStart;
        private float lightLevel;

        public override void OnStart()
        {
            pool = EntityPool.For(Assets.GetAsset<Prefab>(tilePrefab));
            pool.Prewarm(Board.Size * Board.Size + 2);
            cameraRig = cameraEntity ? cameraEntity.GetComponent<CameraRig>() : null;
            sparks = mergeSparks.GetComponent<ParticleEmitterComponent>();
            ring = mergeRing.GetComponent<ParticleEmitterComponent>();
            flash = mergeFlash.GetComponent<ParticleEmitterComponent>();
            glint = spawnGlint.GetComponent<ParticleEmitterComponent>();
            confettiEmitter = confetti.GetComponent<ParticleEmitterComponent>();
            flashLight = mergeLight.GetComponent<LightComponent>();
            LoadSounds();
            hudDocument = hudEntity.GetComponent<UIDocumentComponent>();
            hud = new HudModel(OnNewGameClicked, OnKeepPlayingClicked) { best = SaveData.LoadBest() };
            hud.touch = Input.touchSupported;
            model = hudDocument.GetModel("hud");
            model.Bind(hud);
            model.AddTransform<long>("thousands", v => v.ToString("N0", CultureInfo.InvariantCulture));
            StartCoroutine(BeginGame(true));
        }

        public override void OnUpdate()
        {
            FadeLight();
            if (Input.IsPressed("Restart"))
            {
                NewGame();
                return;
            }
            var move = ReadMove();
            if (move.HasValue && busy)
            {
                queuedMove = move;
            }
            else if (move.HasValue)
            {
                TryMove(move.Value);
            }
            else if (!busy && queuedMove.HasValue)
            {
                var queued = queuedMove.Value;
                queuedMove = null;
                TryMove(queued);
            }
        }

        public override void OnDestroy()
        {
            if (hud != null)
            {
                SaveData.SaveBest(hud.best);
            }
        }

        /// <summary>Clears the board (tiles fly off) and starts over.</summary>
        public void NewGame()
        {
            if (!restarting)
            {
                StartCoroutine(Restart());
            }
        }

        private void OnNewGameClicked()
        {
            Play(clickClip, 0.7f);
            NewGame();
        }

        private void OnKeepPlayingClicked()
        {
            Play(clickClip, 0.7f);
            keepPlaying = true;
            hud.won = false;
            model.Dirty("won");
        }

        private void LoadSounds()
        {
            slideClip = Assets.GetAsset<AudioClip>(slideSound);
            mergeClip = Assets.GetAsset<AudioClip>(mergeSound);
            bigMergeClip = Assets.GetAsset<AudioClip>(bigMergeSound);
            spawnClip = Assets.GetAsset<AudioClip>(spawnSound);
            bumpClip = Assets.GetAsset<AudioClip>(bumpSound);
            winClip = Assets.GetAsset<AudioClip>(winSound);
            gameOverClip = Assets.GetAsset<AudioClip>(gameOverSound);
            clickClip = Assets.GetAsset<AudioClip>(clickSound);
            whooshClip = Assets.GetAsset<AudioClip>(whooshSound);
            Audio.SetClipLimit(slideClip, 2, 0.04f);
            Audio.SetClipLimit(spawnClip, 2, 0.04f);
        }

        // ------------------------------------------------------------------ input

        private Direction? ReadMove()
        {
            if (Input.IsPressed("Move Up"))
            {
                return Direction.Up;
            }
            if (Input.IsPressed("Move Down"))
            {
                return Direction.Down;
            }
            if (Input.IsPressed("Move Left"))
            {
                return Direction.Left;
            }
            if (Input.IsPressed("Move Right"))
            {
                return Direction.Right;
            }
            return ReadSwipe();
        }

        /// <summary>A drag of the left mouse button that did not start on the HUD's controls.</summary>
        private Direction? ReadSwipe()
        {
            if (Input.IsPressed(MouseButton.Left))
            {
                swipeStart = Input.mousePosition;
                swiping = !IsOverHudControl(swipeStart);
            }
            if (!swiping || !Input.IsReleased(MouseButton.Left))
            {
                return null;
            }
            swiping = false;
            var delta = Input.mousePosition - swipeStart;
            float threshold = swipePixels * Mathf.Max(1.0f, Screen.dpi / 96.0f);
            if (delta.magnitude < threshold)
            {
                return null;
            }
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                return delta.x > 0.0f ? Direction.Right : Direction.Left;
            }
            return delta.y > 0.0f ? Direction.Down : Direction.Up;
        }

        private bool IsOverHudControl(Vector2 point)
        {
            var document = hudDocument.document;
            var element = document?.ElementAt(point);
            return element != null && element.Closest("button, .panel") != null;
        }

        // ------------------------------------------------------------------ turns

        private IEnumerator BeginGame(bool intro)
        {
            busy = true;
            gameOver = false;
            keepPlaying = false;
            queuedMove = null;
            board.Clear();
            hud.score = 0;
            hud.newBest = false;
            hud.gameOver = false;
            hud.won = false;
            model.DirtyAll();
            if (intro)
            {
                yield return new WaitForSeconds(1.0f);
                hud.ready = true;
                model.Dirty("ready");
                yield return new WaitForSeconds(0.35f);
            }
            SpawnTile();
            yield return new WaitForSeconds(0.14f);
            SpawnTile();
            yield return new WaitForSeconds(spawnSeconds);
            busy = false;
        }

        private IEnumerator Restart()
        {
            restarting = true;
            busy = true;
            SaveData.SaveBest(hud.best);
            Play(whooshClip, 0.6f);
            int order = 0;
            foreach (var view in views.Values)
            {
                StartCoroutine(FlyAway(view, order++ * 0.03f));
            }
            views.Clear();
            hud.gameOver = false;
            hud.won = false;
            model.Dirty("gameOver");
            model.Dirty("won");
            yield return new WaitForSeconds(0.55f);
            restarting = false;
            yield return StartCoroutine(BeginGame(false));
        }

        private void TryMove(Direction direction)
        {
            if (gameOver || restarting || (hud.won && !keepPlaying))
            {
                return;
            }
            var result = board.Move(direction);
            StartCoroutine(result.Moved ? PlayMove(result) : Bump(direction));
        }

        private IEnumerator PlayMove(MoveResult result)
        {
            busy = true;
            Play(slideClip, 0.45f, Random.Range(0.92f, 1.08f));
            foreach (var tile in result.Slides)
            {
                StartCoroutine(Slide(views[tile], CellPosition(tile.Row, tile.Col)));
            }
            var merged = new List<(TileView survivor, TileView consumed)>();
            foreach (var merge in result.Merges)
            {
                var consumed = views[merge.Consumed];
                views.Remove(merge.Consumed);
                merged.Add((views[merge.Survivor], consumed));
                StartCoroutine(Slide(consumed, CellPosition(merge.Survivor.Row, merge.Survivor.Col)));
            }
            yield return new WaitForSeconds(slideSeconds);
            int topValue = 0;
            foreach (var (survivor, consumed) in merged)
            {
                Release(consumed);
                int value = survivor.Value * 2;
                survivor.Show(value);
                StartCoroutine(Pop(survivor));
                PlayMergeEffects(survivor, value);
                topValue = Math.Max(topValue, value);
            }
            if (topValue > 0)
            {
                PlayMergeSounds(topValue);
            }
            AddScore(result.ScoreGained);
            yield return new WaitForSeconds(0.03f);
            SpawnTile();
            if (!hud.won && !keepPlaying && board.MaxValue >= winValue)
            {
                yield return StartCoroutine(Win());
            }
            else if (!board.CanMove())
            {
                yield return StartCoroutine(GameOver());
            }
            yield return new WaitForSeconds(0.05f);
            busy = false;
        }

        private IEnumerator Win()
        {
            hud.won = true;
            model.Dirty("won");
            Play(winClip, 0.9f);
            cameraRig?.Shake(0.09f);
            for (int burst = 0; burst < 3; ++burst)
            {
                confettiEmitter.Emit(160, new EmitParams { position = new Vector3(Random.Range(-1.5f, 1.5f), 0.4f, Random.Range(-1.0f, 2.0f)) });
                yield return new WaitForSeconds(0.25f);
            }
        }

        private IEnumerator GameOver()
        {
            gameOver = true;
            SaveData.SaveBest(hud.best);
            yield return new WaitForSeconds(0.35f);
            Play(gameOverClip, 0.8f);
            cameraRig?.Shake(0.04f);
            const float dimSeconds = 0.8f;
            for (float t = 0.0f; t < dimSeconds; t += Time.deltaTime)
            {
                float amount = Ease.OutCubic(t / dimSeconds) * 0.85f;
                foreach (var view in views.Values)
                {
                    view.Dim(amount);
                }
                yield return null;
            }
            hud.gameOver = true;
            model.Dirty("gameOver");
        }

        private void AddScore(int gained)
        {
            if (gained <= 0)
            {
                return;
            }
            hud.score += gained;
            model.Dirty("score");
            if (hud.score > hud.best)
            {
                hud.best = hud.score;
                hud.newBest = true;
                model.Dirty("best");
                model.Dirty("newBest");
            }
            var document = hudDocument.document;
            var delta = document?.Q("#score-delta");
            if (delta != null)
            {
                delta.text = "+" + gained.ToString(CultureInfo.InvariantCulture);
                delta.RestartAnimation();
                document.Q("#score-box")?.RestartAnimation();
            }
        }

        // ------------------------------------------------------------------ tiles

        private Vector3 CellPosition(int row, int col)
        {
            float center = (Board.Size - 1) * 0.5f;
            return new Vector3((col - center) * cellSpacing, tileBaseHeight, (center - row) * cellSpacing);
        }

        private void SpawnTile()
        {
            var tile = board.Spawn();
            if (tile == null)
            {
                return;
            }
            var entity = pool.Acquire(tilesRoot);
            if (!viewsByEntity.TryGetValue(entity.Id, out var view))
            {
                view = new TileView(entity);
                viewsByEntity[entity.Id] = view;
            }
            view.Show(tile.Value);
            view.SetScale(0.0f, 0.0f);
            view.Position = CellPosition(tile.Row, tile.Col);
            views[tile] = view;
            StartCoroutine(SpawnIn(view));
        }

        private void Release(TileView view)
        {
            view.SetScale(1.0f, 1.0f);
            pool.Release(view.Root);
        }

        private IEnumerator Slide(TileView view, Vector3 target)
        {
            var start = view.Position;
            for (float t = 0.0f; t < slideSeconds; t += Time.deltaTime)
            {
                view.Position = Vector3.Lerp(start, target, Ease.OutCubic(t / slideSeconds));
                yield return null;
            }
            view.Position = target;
        }

        private IEnumerator SpawnIn(TileView view)
        {
            Play(spawnClip, 0.35f, Random.Range(0.95f, 1.12f));
            var target = view.Position;
            glint.Emit(1, new EmitParams { position = target + new Vector3(0.0f, 0.55f, 0.0f), color = view.Style.Body });
            for (float t = 0.0f; t < spawnSeconds; t += Time.deltaTime)
            {
                float k = t / spawnSeconds;
                float size = Ease.OutBack(k, 2.2f);
                view.SetScale(size, size);
                view.Position = target + new Vector3(0.0f, (1.0f - Ease.OutCubic(k)) * 0.7f, 0.0f);
                yield return null;
            }
            view.SetScale(1.0f, 1.0f);
            view.Position = target;
        }

        /// <summary>Squash and stretch: the merged tile jumps up, wider and taller, then settles.</summary>
        private IEnumerator Pop(TileView view)
        {
            for (float t = 0.0f; t < popSeconds; t += Time.deltaTime)
            {
                float k = t / popSeconds;
                view.SetScale(1.0f + 0.2f * Ease.Bump(k), 1.0f + 0.55f * Ease.Bump(k) - 0.15f * Ease.Wobble(k));
                yield return null;
            }
            view.SetScale(1.0f, 1.0f);
        }

        private IEnumerator FlyAway(TileView view, float delay)
        {
            yield return new WaitForSeconds(delay);
            var start = view.Position;
            const float seconds = 0.32f;
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                float k = Ease.InCubic(t / seconds);
                view.Position = start + new Vector3(0.0f, k * 3.0f, 0.0f);
                view.SetScale(1.0f - k, 1.0f - k);
                yield return null;
            }
            Release(view);
        }

        /// <summary>A move that changes nothing: the board nudges that way and back with a thud.</summary>
        private IEnumerator Bump(Direction direction)
        {
            busy = true;
            Play(bumpClip, 0.55f);
            var offset = DirectionVector(direction) * 0.14f;
            var boardStart = boardRoot.transform.position;
            var tilesStart = tilesRoot.transform.position;
            const float seconds = 0.26f;
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                var shift = offset * Ease.Wobble(t / seconds);
                boardRoot.transform.position = boardStart + shift;
                tilesRoot.transform.position = tilesStart + shift;
                yield return null;
            }
            boardRoot.transform.position = boardStart;
            tilesRoot.transform.position = tilesStart;
            busy = false;
        }

        private static Vector3 DirectionVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new Vector3(0.0f, 0.0f, 1.0f);
                case Direction.Down: return new Vector3(0.0f, 0.0f, -1.0f);
                case Direction.Left: return new Vector3(-1.0f, 0.0f, 0.0f);
                default: return new Vector3(1.0f, 0.0f, 0.0f);
            }
        }

        // ------------------------------------------------------------------ effects

        private static int Level(int value)
        {
            int level = 0;
            while (value > 1)
            {
                value >>= 1;
                ++level;
            }
            return level;
        }

        private void PlayMergeEffects(TileView view, int value)
        {
            int level = Level(value);
            var top = view.Position + new Vector3(0.0f, 0.42f, 0.0f);
            var color = view.Style.Body;
            sparks.Emit(10 + level * 4, new EmitParams { position = top, color = color });
            ring.Emit(1, new EmitParams { position = view.Position + new Vector3(0.0f, 0.4f, 0.0f), color = color });
            flash.Emit(1, new EmitParams { position = top, color = color });
            flashLight.color = color;
            mergeLight.transform.position = top + new Vector3(0.0f, 0.8f, 0.0f);
            lightLevel = Mathf.Max(lightLevel, 3.0f + level * 1.5f);
            if (value >= BigMergeValue)
            {
                cameraRig?.Shake(0.02f + 0.012f * (level - Level(BigMergeValue)));
            }
        }

        private void FadeLight()
        {
            if (flashLight == null)
            {
                return;
            }
            lightLevel *= Mathf.Exp(-LightFlashDecay * Time.deltaTime);
            flashLight.intensity = lightLevel;
        }

        /// <summary>Each doubling plays the merge a step higher on a pentatonic scale, so long chains sound like a melody.</summary>
        private void PlayMergeSounds(int topValue)
        {
            int step = Math.Max(0, Level(topValue) - 2);
            int semitones = PentatonicSteps[step % PentatonicSteps.Length] + 12 * (step / PentatonicSteps.Length);
            float pitch = 0.75f * Mathf.Pow(2.0f, semitones / 12.0f);
            Play(mergeClip, 0.75f, Mathf.Clamp(pitch, 0.5f, 4.0f));
            if (topValue >= BigMergeValue)
            {
                Play(bigMergeClip, 0.35f + 0.05f * (Level(topValue) - Level(BigMergeValue)));
            }
        }

        private static void Play(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            if (clip != null)
            {
                Audio.PlayOneShot(clip, volume, pitch);
            }
        }
    }
}
