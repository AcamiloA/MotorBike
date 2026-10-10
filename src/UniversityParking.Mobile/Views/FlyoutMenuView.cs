using System.ComponentModel;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Views;

public sealed class FlyoutMenuView : ContentView
{
    private readonly FlyoutMenuViewModel model;
    private readonly List<(NavigationItem Item, Border Row, Label Text, Button Button)> items = [];

    public FlyoutMenuView(FlyoutMenuViewModel model)
    {
        this.model = model;
        BindingContext = model;
        SetDynamicResource(BackgroundColorProperty, "Surface");
        var resources = Application.Current!.Resources;
        var header = new VerticalStackLayout { Padding = new Thickness(20, 20, 20, 12), Spacing = 6 };
        header.Add(new Label { Text = "MOTOBIKE PARK", Style = (Style)resources["BrandTitle"] });
        header.Add(new Label { Text = model.UserName, FontAttributes = FontAttributes.Bold });
        header.Add(new Label { Text = model.Roles, Style = (Style)resources["Caption"] });
        var body = new VerticalStackLayout { Padding = new Thickness(12, 0), Spacing = 8 };
        if (model.Home is not null) body.Add(Item(model.Home));
        foreach (var section in model.Sections)
        {
            var (row, heading, toggle) = Row(section.Icon, model.ToggleCommand, section);
            heading.BindingContext = section;
            heading.FontAttributes = FontAttributes.Bold;
            heading.SetBinding(Label.TextProperty, nameof(NavigationSection.Heading));
            SemanticProperties.SetDescription(toggle, $"Expandir o contraer {section.Title}");
            body.Add(row);
            var children = new VerticalStackLayout { BindingContext = section, Spacing = 4, Padding = new Thickness(0, 0, 0, 4) };
            children.SetBinding(IsVisibleProperty, nameof(NavigationSection.IsExpanded));
            foreach (var item in section.Items) children.Add(Item(item));
            body.Add(children);
        }
        var error = new Label();
        error.SetBinding(Label.TextProperty, nameof(FlyoutMenuViewModel.ErrorMessage));
        error.SetDynamicResource(Label.TextColorProperty, "Danger");
        body.Add(error);
        var logout = new Button { Text = "CERRAR SESIÓN", Command = model.LogoutCommand,
            Style = (Style)resources["SecondaryButton"], Margin = new Thickness(12) };
        var layout = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star }, new RowDefinition { Height = GridLength.Auto } } };
        layout.Add(header);
        var scroll = new ScrollView { Content = body }; layout.Add(scroll); Grid.SetRow(scroll, 1);
        layout.Add(logout); Grid.SetRow(logout, 2); Content = layout;
    }

    private static (Border Row, Label Text, Button Button) Row(string icon, System.Windows.Input.ICommand command, object parameter)
    {
        var grid = new Grid { Padding = new Thickness(12, 8), ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(28), new ColumnDefinition(GridLength.Star) }, HeightRequest = 56 };
        var image = new Image { Source = icon, WidthRequest = 24, HeightRequest = 24, Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        var text = new Label { VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Start,
            LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2, InputTransparent = true };
        text.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        grid.Add(image); grid.Add(text, 1);
        var row = new Border { StrokeThickness = 0, Content = grid };
        row.SetDynamicResource(BackgroundColorProperty, "Surface");
        // A transparent button preserves command/CanExecute and accessibility behavior.
        var button = new Button { Text = "", Command = command, CommandParameter = parameter, BackgroundColor = Colors.Transparent,
            BorderWidth = 0, Padding = 0, Margin = new Thickness(-12, -8) };
        var transparentStates = new VisualStateGroup { Name = "CommonStates" };
        foreach (var name in new[] { "Normal", "Pressed", "Disabled" })
        {
            var state = new VisualState { Name = name };
            state.Setters.Add(new Setter { Property = BackgroundColorProperty, Value = Colors.Transparent });
            transparentStates.States.Add(state);
        }
        VisualStateManager.SetVisualStateGroups(button, new VisualStateGroupList { transparentStates });
        grid.Add(button); Grid.SetColumnSpan(button, 2);
        return (row, text, button);
    }
    private Border Item(NavigationItem item)
    {
        var (row, text, button) = Row(item.Icon, model.NavigateCommand, item);
        text.Text = item.Title;
        SemanticProperties.SetDescription(button, item.Title);
        items.Add((item, row, text, button));
        return row;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        model.PropertyChanged -= Changed;
        if (Handler is not null) { model.PropertyChanged += Changed; Refresh(); }
    }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(FlyoutMenuViewModel.ActiveKey)) Refresh(); }
    private void Refresh()
    {
        foreach (var (item, row, text, button) in items)
        {
            var active = item.Key == model.ActiveKey;
            row.SetDynamicResource(BackgroundColorProperty, active ? "PrimaryDark" : "Surface");
            text.SetDynamicResource(Label.TextColorProperty, active ? "Primary" : "TextPrimary");
            SemanticProperties.SetDescription(button, active ? $"{item.Title}, seleccionado" : item.Title);
        }
    }
}
