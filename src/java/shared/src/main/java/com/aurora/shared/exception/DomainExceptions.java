package com.aurora.shared.exception;

/**
 * Domain Exception hierarchy matching C# DomainExceptions.cs
 */
public class DomainExceptions {

    public static class DomainException extends RuntimeException {
        public DomainException(String message) {
            super(message);
        }
        public DomainException(String message, Throwable cause) {
            super(message, cause);
        }
    }

    public static class NotFoundException extends DomainException {
        public NotFoundException(String message) {
            super(message);
        }
    }

    public static class ConflictException extends DomainException {
        public ConflictException(String message) {
            super(message);
        }
    }

    public static class ForbiddenException extends DomainException {
        public ForbiddenException(String message) {
            super(message);
        }
    }
}
