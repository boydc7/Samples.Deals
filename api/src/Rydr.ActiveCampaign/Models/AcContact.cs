using ServiceStack;

namespace Rydr.ActiveCampaign.Models;

public class AcContact
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public string Deleted { get; set; }

    public bool IsDeleted => !Deleted.IsNullOrEmpty() && Deleted.Equals("1", StringComparison.Ordinal);
}

public class GetAcContacts : AcCollectionBase<AcContact>
{
    public IReadOnlyList<AcContact> Contacts { get; set; }

    public override IReadOnlyList<AcContact> Data => Contacts;
}

public class GetAcContact
{
    public AcContact Contact { get; set; }
}

public class PostAcContact
{
    public AcContact Contact { get; set; }
}
