namespace MzukuluQMS.Api.Services.Workflow;
public sealed record SignatureCandidate
{
    public string SignOffType {get;init;}="";
    public int HashVersion {get;init;}
    public string ContentHash {get;init;}="";
    public DateTime SignedAt {get;init;}
    public DateTime EvidenceCreatedAt {get;init;}
    public long? QCFormFieldID {get;init;}
    public bool IsActive {get;init;}
    public bool HasAuthority {get;init;}
    public bool HasProjectAccess {get;init;}
    public bool HasValidEvidence {get;init;}
}
public static class SignatureRules
{
    public static bool IsCurrent(SignatureCandidate signature,string currentHash,DateTime submittedAt) =>
        signature.HashVersion==2 && string.Equals(signature.ContentHash,currentHash,StringComparison.Ordinal)
        && signature.SignedAt>=submittedAt && signature.EvidenceCreatedAt>=submittedAt
        && signature.IsActive && signature.HasAuthority && signature.HasProjectAccess && signature.HasValidEvidence;

    public static void RequireCurrent(IReadOnlyCollection<string> requiredTypes,IReadOnlyCollection<long> requiredFields,
        IReadOnlyCollection<SignatureCandidate> history,string currentHash,DateTime? submittedAt)
    {
        if(requiredTypes.Count==0)throw new InvalidOperationException("Required signature policy has not been configured for this form.");
        if(submittedAt is null)throw new InvalidOperationException("The form has no submitted signature context.");
        var current=history.Where(s=>IsCurrent(s,currentHash,submittedAt.Value)).ToList();
        if(requiredTypes.Any(type=>!current.Any(s=>string.Equals(s.SignOffType,type,StringComparison.Ordinal))))
            throw new InvalidOperationException("Approval requires fresh authorised signatures matching current content and all required signoff types.");
        if(requiredFields.Any(field=>!current.Any(s=>s.QCFormFieldID==field)))
            throw new InvalidOperationException("Required signature fields need current authorised signoffs with matching field evidence.");
    }
}
