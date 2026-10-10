namespace UniversityParking.Mobile.Core;
public static class UniversityCatalogPresentation
{
    public static string Notice(bool busy,bool loaded,int count,string? error)
    {
        if (!loaded && busy) return "Cargando universidades…";
        if (!loaded && !string.IsNullOrWhiteSpace(error)) return "No pudimos cargar las universidades.";
        return loaded && count == 0 ? "No hay universidades activas disponibles." : "";
    }
}
