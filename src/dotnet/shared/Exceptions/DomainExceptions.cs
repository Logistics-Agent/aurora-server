namespace Shared.Exceptions;

public class DomainException(string message) : Exception(message);
public class NotFoundException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public class ForbiddenException(string message) : Exception(message);
public class DomainValidationException(string message) : DomainException(message);
public class PolicyUnavailableException(string message) : Exception(message);
public class RiskPolicyNotConfiguredException(string message) : DomainException(message);

