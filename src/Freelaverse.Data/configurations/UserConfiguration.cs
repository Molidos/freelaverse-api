using FreelaverseApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        //* Quando o usuário for excluído, excluo os serviços solicitados por ele
        builder.HasMany(u => u.ClientServices)
            .WithOne(s => s.Client)
            .HasForeignKey(s => s.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        //* Usuário do tipo profissional e serviços prestados - Não vou excluir quando o usuário for excluído
        builder.HasMany(u => u.ProfessionalService)
            .WithOne(ps => ps.Professional)
            .HasForeignKey(ps => ps.ProfessionalId)
            .OnDelete(DeleteBehavior.Restrict);
        
        //* Áreas de atuação e expertise do usuário do tipo profissional
        builder.HasMany(u => u.UserProfessionalArea)
            .WithOne(upa => upa.User)
            .HasForeignKey(upa => upa.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}