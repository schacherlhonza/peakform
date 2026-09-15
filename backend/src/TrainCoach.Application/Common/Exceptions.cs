namespace TrainCoach.Application.Common;

/// <summary>Caller is authenticated but not allowed to touch this resource — maps to HTTP 403.
/// Every cross-user data access path that fails this check must throw it, never a bare 404,
/// except where deliberately returning 404 avoids confirming a resource's existence.</summary>
public class ForbiddenAccessException(string message) : Exception(message);

/// <summary>Requested resource does not exist (or is soft-deleted) — maps to HTTP 404.</summary>
public class NotFoundException(string entityName, object key)
    : Exception($"Entita \"{entityName}\" ({key}) nebyla nalezena.");

/// <summary>A business rule was violated (not a field-validation error) — maps to HTTP 409/400.</summary>
public class BusinessRuleException(string message) : Exception(message);
