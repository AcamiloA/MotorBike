using System.Text;
using System.Globalization;

namespace UniversityParking.Infrastructure.Persistence.Seeding;

internal static class DemoSeedAssets
{
    // Small valid PNG placeholder, explicitly a demonstration asset, not a vehicle photograph.
    public static byte[] Photo => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGPY/p8BAAQnAbfouy2QAAAAAElFTkSuQmCC");
    public static byte[] Document
    {
        get
        {
            const string content = "BT /F1 18 Tf 40 740 Td (DEMO - SIN VALIDEZ LEGAL) Tj ET";
            var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {content.Length} >>\nstream\n{content}\nendstream" };
            var result = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (var index = 0; index < objects.Length; index++)
            {
                offsets.Add(result.Length);
                result.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            }
            var xref = result.Length;
            result.Append("xref\n0 6\n0000000000 65535 f \n");
            foreach (var offset in offsets) result.Append(offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
            result.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return Encoding.ASCII.GetBytes(result.ToString());
        }
    }
}
