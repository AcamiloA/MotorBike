namespace UniversityParking.Mobile.Controls;

// MAUI owns the live system/IME insets. Apply them once at the page boundary,
// leaving ScrollView bounded by the remaining space instead of adding padding.
internal static class InputViewport
{
    public static bool ContainsInput(View view) => view switch
    {
        Entry or Editor or SearchBar or PasswordField => true,
        Layout layout => layout.Children.OfType<View>().Any(ContainsInput),
        ContentView content when content.Content is View child => ContainsInput(child),
        Border border when border.Content is View child => ContainsInput(child),
        ScrollView scroll when scroll.Content is View child => ContainsInput(child),
        _ => false
    };

    public static void Configure(ContentPage page, View body, View? footer = null)
    {
        if (ContainsInput(body) || footer is not null && ContainsInput(footer))
            page.SafeAreaEdges = SafeAreaEdges.All;
    }

    public static void ClearFocus(View view)
    {
        view.Unfocus();
        if (view is PasswordField password) password.InputEntry.Unfocus();
        else if (view is Layout layout) foreach (var child in layout.Children.OfType<View>()) ClearFocus(child);
        else if (view is ContentView content && content.Content is View child) ClearFocus(child);
        else if (view is Border border && border.Content is View borderChild) ClearFocus(borderChild);
        else if (view is ScrollView scroll && scroll.Content is View scrollChild) ClearFocus(scrollChild);
    }
}
