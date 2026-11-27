namespace Newsletters.Domain.Models;

public class Admin
{
    private Admin() { }

    public Admin(string login, string passwordHash, string name, string email)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length > 80)
            throw new ArgumentException("Login is required and must be at most 80 characters.", nameof(login));
        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > 255)
            throw new ArgumentException("Password hash is required and must be at most 255 characters.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            throw new ArgumentException("Name is required and must be at most 120 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            throw new ArgumentException("Email is required and must be at most 254 characters.", nameof(email));

        Login = login;
        PasswordHash = passwordHash;
        Name = name;
        Email = email;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = login;
        RowVersion = 1;
    }

    public long Id { get; private set; }
    public string Login { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public long RowVersion { get; private set; }
    public ICollection<Newsletter> Newsletters { get; } = new List<Newsletter>();
}
