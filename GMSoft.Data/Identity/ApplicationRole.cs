using Microsoft.AspNetCore.Identity;

namespace GMSoft.Data.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string name) : base(name) { }
}
