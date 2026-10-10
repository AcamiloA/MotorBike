using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Tests;
public sealed class UniversityCatalogPresentationTests
{
    [Theory]
    [InlineData(true,false,0,"","Cargando universidades…")]
    [InlineData(false,false,0,"error","No pudimos cargar las universidades.")]
    [InlineData(false,true,0,"","No hay universidades activas disponibles.")]
    [InlineData(true,true,1,"error al guardar","")]
    [InlineData(false,false,0,"","")]
    public void CatalogStatusDoesNotMislabelFormErrors(bool busy,bool loaded,int count,string error,string expected)
    {Assert.Equal(expected,UniversityCatalogPresentation.Notice(busy,loaded,count,error));}
}
