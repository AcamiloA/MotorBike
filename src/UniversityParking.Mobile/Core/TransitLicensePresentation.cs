namespace UniversityParking.Mobile.Core;

public static class TransitLicensePresentation
{
    public static bool DocumentError(string code) => code is "TRANSIT_LICENSE_REVIEW_REQUIRED" or "TRANSIT_LICENSE_INVALID_FORMAT" or "TRANSIT_LICENSE_UNREADABLE";
}
