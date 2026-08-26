using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WhatsAppBot.AdminPanel.Services;

// Deliberadamente el ÚNICO acceso a base de datos de todo el panel — no
// tiene ninguna tabla de negocio, solo la que necesita Data Protection
// para guardar sus claves de cifrado. Ver el comentario en el .csproj.
public class DataProtectionKeysDbContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options) : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
