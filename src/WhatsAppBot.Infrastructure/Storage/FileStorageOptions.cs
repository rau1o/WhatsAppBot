namespace WhatsAppBot.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    // Carpeta física donde se guardan los archivos — relativa al directorio de ejecución.
    public string LocalPath { get; set; } = "App_Data/uploads";

    // Prefijo con el que Api sirve esos archivos como estáticos (ver Program.cs).
    public string PublicBaseUrl { get; set; } = "/uploads";
}
