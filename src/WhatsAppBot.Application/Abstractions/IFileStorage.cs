namespace WhatsAppBot.Application.Abstractions;

// Puerto: Application solo sabe que puede "guardar un archivo y obtener
// una URL para acceder a él". Hoy Infrastructure lo implementa guardando
// en disco local — el día que se mueva a Azure Blob/S3, esta interfaz
// no cambia.
public interface IFileStorage
{
    Task<string> SaveAsync(byte[] content, string fileName, string contentType, CancellationToken ct);
}
