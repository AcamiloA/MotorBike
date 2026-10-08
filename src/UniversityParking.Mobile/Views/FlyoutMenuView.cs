using System.ComponentModel;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Views;

public sealed class FlyoutMenuView : ContentView
{
    private readonly FlyoutMenuViewModel model;
    private readonly List<(NavigationItem Item, Button Button)> items = [];

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
            var toggle = new Button { BindingContext = section, Style = (Style)resources["FlyoutSection"],
                Command = model.ToggleCommand, CommandParameter = section, ImageSource = section.Icon,
                ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 12) };
            toggle.SetBinding(Button.TextProperty, nameof(NavigationSection.Heading));
            SemanticProperties.SetDescription(toggle, $"Expandir o contraer {section.Title}");
            body.Add(toggle);
            var children = new VerticalStackLayout { BindingContext = section, Spacing = 4, Padding = new Thickness(12, 0, 0, 4) };
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

    private Button Item(NavigationItem item)
    {
        var button = new Button { Text = item.Title, ImageSource = item.Icon,
            ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 12),
            Command = model.NavigateCommand, CommandParameter = item,
            Style = (Style)Application.Current!.Resources["FlyoutItem"] };
        items.Add((item, button));
        return button;
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
        foreach (var (item, button) in items)
        {
            var active = item.Key == model.ActiveKey;
            button.Text = active ? $"› {item.Title}" : item.Title;
            button.SetDynamicResource(Button.BackgroundColorProperty, active ? "PrimaryDark" : "Surface");
            button.SetDynamicResource(Button.TextColorProperty, active ? "Primary" : "TextPrimary");
            SemanticProperties.SetDescription(button, active ? $"{item.Title}, seleccionado" : item.Title);
        }
    }
}
