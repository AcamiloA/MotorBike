namespace UniversityParking.Mobile.Controls;

internal sealed class VehicleDeleteConfirmation : ContentPage
{
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool accepted, closing;
    public Task<bool> Result => completion.Task;

    public VehicleDeleteConfirmation(string message)
    {
        Title = "¿Eliminar vehículo?";
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
        var cancel = new Button { Text = "CANCELAR", HorizontalOptions = LayoutOptions.Start };
        var delete = new Button { Text = "ELIMINAR", HorizontalOptions = LayoutOptions.Start };
        cancel.SetDynamicResource(StyleProperty, "SecondaryButton");
        delete.SetDynamicResource(StyleProperty, "DangerButton");
        cancel.Clicked += async (_, _) => await CloseAsync(false);
        delete.Clicked += async (_, _) => await CloseAsync(true);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 16,
            Children = { new Label { Text = Title, FontSize = 21 }, new Label { Text = message }, cancel, delete } } };
    }

    private async Task CloseAsync(bool confirmed)
    {
        if (closing) return;
        closing = true;
        accepted = confirmed;
        try { await Navigation.PopModalAsync(); }
        catch { accepted = false; closing = false; throw; }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        completion.TrySetResult(accepted);
    }
}
