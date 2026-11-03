using Unravel.Core;

namespace Sample2048
{
    /// <summary>How a value looks: the body colour, how strongly it glows and the colour of its digits.</summary>
    public struct TileStyle
    {
        public Color Body;
        public float Glow;
        public Color Text;
    }

    /// <summary>The classic warm ramp up to 64, then tiles that glow: gold, orange, green, blue, violet, pink and teal.</summary>
    public static class TilePalette
    {
        private static readonly Color DarkText = new Color(0.30f, 0.24f, 0.21f, 1.0f);
        private static readonly Color LightText = new Color(1.0f, 0.98f, 0.95f, 1.0f);

        public static TileStyle For(int value)
        {
            switch (value)
            {
                case 2: return Style(0.92f, 0.88f, 0.82f, 0.0f, DarkText);
                case 4: return Style(0.94f, 0.80f, 0.56f, 0.0f, DarkText);
                case 8: return Style(0.99f, 0.55f, 0.22f, 0.0f, LightText);
                case 16: return Style(0.99f, 0.40f, 0.17f, 0.0f, LightText);
                case 32: return Style(0.97f, 0.27f, 0.24f, 0.0f, LightText);
                case 64: return Style(0.86f, 0.11f, 0.17f, 0.05f, LightText);
                case 128: return Style(0.99f, 0.80f, 0.26f, 0.22f, new Color(0.45f, 0.24f, 0.04f, 1.0f));
                case 256: return Style(0.99f, 0.65f, 0.12f, 0.38f, new Color(0.40f, 0.16f, 0.02f, 1.0f));
                case 512: return Style(0.32f, 0.86f, 0.36f, 0.45f, new Color(0.04f, 0.24f, 0.07f, 1.0f));
                case 1024: return Style(0.16f, 0.70f, 0.98f, 0.55f, new Color(0.02f, 0.14f, 0.34f, 1.0f));
                case 2048: return Style(0.50f, 0.20f, 0.95f, 0.6f, LightText);
                case 4096: return Style(1.0f, 0.24f, 0.64f, 0.95f, LightText);
                default: return Style(0.20f, 0.95f, 0.85f, 1.1f, new Color(0.02f, 0.22f, 0.20f, 1.0f));
            }
        }

        private static TileStyle Style(float r, float g, float b, float glow, Color text)
        {
            return new TileStyle { Body = new Color(r, g, b, 1.0f), Glow = glow, Text = text };
        }
    }

    /// <summary>A pooled tile entity (Prefabs/Tile.pfb): its root moves and scales, Body shows the colour, Label the value.</summary>
    public sealed class TileView
    {
        public readonly Entity Root;
        private readonly ModelComponent body;
        private readonly TextComponent label;

        public TileView(Entity root)
        {
            Root = root;
            body = root.transform.FindChild("Body").GetComponent<ModelComponent>();
            label = root.transform.FindChild("Label").GetComponent<TextComponent>();
        }

        public int Value { get; private set; }

        public TileStyle Style { get; private set; }

        public Vector3 Position
        {
            get { return Root.transform.position; }
            set { Root.transform.position = value; }
        }

        public void SetScale(float horizontal, float vertical)
        {
            Root.transform.localScale = new Vector3(horizontal, vertical, horizontal);
        }

        public void Show(int value)
        {
            Value = value;
            Style = TilePalette.For(value);
            body.GetMaterialInstance().baseColor = Style.Body;
            body.GetMaterialInstance().emissiveColor = Style.Body;
            body.GetMaterialInstance().emissiveIntensity = Style.Glow;
            string digits = value.ToString();
            label.text = digits;
            label.color = Style.Text;
            label.opacity = 1.0f;
            label.fontSize = FontSizeFor(digits.Length);
        }

        /// <summary>Fades the tile towards the board colour for the game-over screen; 0 is the normal look.</summary>
        public void Dim(float amount)
        {
            var board = new Color(0.12f, 0.11f, 0.24f, 1.0f);
            body.GetMaterialInstance().baseColor = Color.Lerp(Style.Body, board, amount * 0.7f);
            body.GetMaterialInstance().emissiveIntensity = Style.Glow * (1.0f - amount);
            label.opacity = 1.0f - amount * 0.55f;
        }

        private static uint FontSizeFor(int digits)
        {
            switch (digits)
            {
                case 1:
                case 2:
                    return 7;
                case 3:
                    return 6;
                case 4:
                    return 5;
                default:
                    return 4;
            }
        }
    }
}
