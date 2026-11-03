using System;

namespace Sample2048
{
    /// <summary>
    /// The data model "hud" of UI/Hud.rhtml: the scores and which panel shows. The buttons call NewGame and
    /// KeepPlaying (data-event-click), which hand the request to the game.
    /// </summary>
    public sealed class HudModel
    {
        public long score;
        public long best;
        public bool newBest;
        public bool ready;
        /// <summary>A touch screen: the hint leaves out the keys.</summary>
        public bool touch;
        public bool gameOver;
        public bool won;
        private readonly Action newGame;
        private readonly Action keepPlaying;

        public HudModel(Action newGame, Action keepPlaying)
        {
            this.newGame = newGame;
            this.keepPlaying = keepPlaying;
        }

        public void NewGame()
        {
            newGame();
        }

        public void KeepPlaying()
        {
            keepPlaying();
        }
    }
}
