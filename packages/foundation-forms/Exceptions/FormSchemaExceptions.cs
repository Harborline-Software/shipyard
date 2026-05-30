using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Forms.Models;

namespace Sunfish.Foundation.Forms.Exceptions;

/// <summary>
/// Raised when <see cref="IFormSchemaRegistry"/> is asked for a schema
/// that does not exist (or does not exist at the requested version /
/// within the requested tenant boundary).
/// </summary>
public sealed class FormSchemaNotFoundException : Exception
{
    /// <summary>The id that was looked up.</summary>
    public FormSchemaId SchemaId { get; }

    /// <summary>The version that was looked up, or <see langword="null"/>
    /// when the caller asked for the "current published" version.</summary>
    public SemanticVersion? Version { get; }

    /// <summary>The tenant boundary the lookup was scoped to.</summary>
    public TenantId Tenant { get; }

    /// <summary>Constructs the exception.</summary>
    public FormSchemaNotFoundException(FormSchemaId schemaId, SemanticVersion? version, TenantId tenant)
        : base(version is null
            ? $"No published FormSchema with id '{schemaId}' in tenant '{tenant}'."
            : $"No FormSchema with id '{schemaId}' at version '{version}' in tenant '{tenant}'.")
    {
        SchemaId = schemaId;
        Version = version;
        Tenant = tenant;
    }
}

/// <summary>
/// Raised when <see cref="IFormSchemaRegistry.RegisterAsync"/> is called
/// for a (tenant, id, version) tuple that already exists. Schema revisions
/// are immutable — overwriting an existing version is a programming error;
/// to ship a corrected revision, register a new version.
/// </summary>
public sealed class FormSchemaConflictException : Exception
{
    /// <summary>The id that conflicted.</summary>
    public FormSchemaId SchemaId { get; }

    /// <summary>The version that conflicted.</summary>
    public SemanticVersion Version { get; }

    /// <summary>The tenant the conflict occurred within.</summary>
    public TenantId Tenant { get; }

    /// <summary>Constructs the exception.</summary>
    public FormSchemaConflictException(FormSchemaId schemaId, SemanticVersion version, TenantId tenant)
        : base($"FormSchema '{schemaId}' at version '{version}' is already registered in tenant '{tenant}'. Register a new version instead of overwriting.")
    {
        SchemaId = schemaId;
        Version = version;
        Tenant = tenant;
    }
}

/// <summary>
/// Raised when a registration attempt violates an overlay invariant — a
/// section references a field that does not appear in the overlay, two
/// sections share an id, a rule scope references a missing field, etc.
/// The exception message names the specific invariant violated.
/// </summary>
public sealed class FormSchemaValidationException : Exception
{
    /// <summary>The id that failed validation.</summary>
    public FormSchemaId SchemaId { get; }

    /// <summary>Constructs the exception.</summary>
    public FormSchemaValidationException(FormSchemaId schemaId, string message)
        : base($"FormSchema '{schemaId}' failed overlay validation: {message}")
    {
        SchemaId = schemaId;
    }
}
