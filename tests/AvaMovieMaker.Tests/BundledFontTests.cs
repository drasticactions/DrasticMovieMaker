using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaMovieMaker.Controls;
using AvaMovieMaker.Views;

namespace AvaMovieMaker.Tests;

public sealed class BundledFontTests
{
    private static string Resolve(FontFamily family, FontWeight weight = FontWeight.Normal, FontStyle style = FontStyle.Normal)
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(family, style, weight), out GlyphTypeface? face));
        return face!.FamilyName;
    }

    [AvaloniaFact]
    public void Timeline_and_storyboard_text_use_the_bundled_interface_font()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            TestApp.Pump();
            var controls = w.GetVisualDescendants().Where(v => v is TimelineControl or StoryboardStrip).ToList();
            Assert.NotEmpty(controls);
            foreach (Avalonia.Visual v in controls)
            {
                Assert.Equal("Selawik", Resolve(TextElement.GetFontFamily((Avalonia.Controls.Control)v)));
            }
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Title_editor_buttons_use_the_bundled_serif()
    {
        Assert.Equal("Liberation Serif", Resolve(BundledFonts.Serif, FontWeight.Bold));
        Assert.Equal("Liberation Serif", Resolve(BundledFonts.Serif, FontWeight.Bold, FontStyle.Italic));
    }
}
