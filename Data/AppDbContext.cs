using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Models;

namespace VicdogsPeru.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Servicio> Servicios { get; set; }
    public DbSet<Tutor> Tutores { get; set; }
    public DbSet<Canino> Caninos { get; set; }
    public DbSet<CaninoTutor> CaninoTutores { get; set; }
    public DbSet<Vacuna> Vacunas { get; set; }
    public DbSet<Antipulga> Antipulgas { get; set; }
    public DbSet<Desparasitacion> Desparasitaciones { get; set; }
    public DbSet<Recordatorio> Recordatorios { get; set; }
    public DbSet<PesoRegistro> Pesos { get; set; }
    public DbSet<Empleado> Empleados { get; set; }
    public DbSet<Asistencia> Asistencias { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Usuario>().HasIndex(u => u.NombreUsuario).IsUnique();

        mb.Entity<CaninoTutor>().HasKey(x => new { x.CaninoId, x.TutorId });
        mb.Entity<CaninoTutor>().HasOne(x => x.Canino).WithMany(c => c.Tutores)
            .HasForeignKey(x => x.CaninoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<CaninoTutor>().HasOne(x => x.Tutor).WithMany(t => t.Caninos)
            .HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Canino>().HasOne(c => c.Servicio).WithMany(s => s.Caninos)
            .HasForeignKey(c => c.ServicioId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Canino>().Property(c => c.PesoKg).HasPrecision(6, 2);
        mb.Entity<Canino>().HasIndex(c => c.Nombre);

        mb.Entity<Vacuna>().HasOne(v => v.Canino).WithMany(c => c.Vacunas)
            .HasForeignKey(v => v.CaninoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Antipulga>().HasOne(a => a.Canino).WithMany(c => c.Antipulgas)
            .HasForeignKey(a => a.CaninoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PesoRegistro>().Property(p => p.PesoKg).HasPrecision(6, 2);
        mb.Entity<PesoRegistro>().HasOne(p => p.Canino).WithMany(c => c.Pesos)
            .HasForeignKey(p => p.CaninoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Asistencia>().Property(a => a.Fecha).HasColumnType("date");
        mb.Entity<Asistencia>().HasIndex(a => new { a.EmpleadoId, a.Fecha }).IsUnique();
        mb.Entity<Asistencia>().HasOne(a => a.Empleado).WithMany(e => e.Asistencias)
            .HasForeignKey(a => a.EmpleadoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Recordatorio>().HasOne(r => r.Canino).WithMany()
            .HasForeignKey(r => r.CaninoId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Recordatorio>().HasOne(r => r.Tutor).WithMany()
            .HasForeignKey(r => r.TutorId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Recordatorio>().HasOne(r => r.Empleado).WithMany()
            .HasForeignKey(r => r.EmpleadoId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Desparasitacion>().HasOne(d => d.Canino).WithMany(c => c.Desparasitaciones)
            .HasForeignKey(d => d.CaninoId).OnDelete(DeleteBehavior.Cascade);
    }
}
