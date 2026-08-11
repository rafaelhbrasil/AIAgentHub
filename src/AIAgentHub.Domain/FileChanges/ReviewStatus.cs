namespace AIAgentHub.Domain.FileChanges;

public enum FileChangeType
{
    Modified = 0,
    Created = 1,
    Deleted = 2
}

public enum ReviewStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}
