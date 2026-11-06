using System;
using System.ComponentModel;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Used by the code the payload generator writes: turns mistakes of user field attributes into clear errors at the
    /// place they happen, instead of a TypeInitializationException that breaks the whole package or a field that fails
    /// later.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class GeneratedFieldGuard
    {
        /// <summary>Creates a user attribute instance; a constructor that throws is reported with the member it serves.</summary>
        public static TAttribute CreateAttribute<TAttribute>(Func<TAttribute> create, string usage)
        {
            try
            {
                return create();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"[{typeof(TAttribute).Name}] on {usage} could not be created: {e.Message}", e);
            }
        }

        /// <summary>Checks the field a user factory created: not null, owned by the package it is created for.</summary>
        public static TField FromFactory<TField, T>(TField field, IPersistencePackage owner, string factory, string usage)
            where TField : PersistentField<T>
        {
            if (field == null)
                throw new InvalidOperationException($"[{factory}] on {usage} returned no field.");

            if (!ReferenceEquals(field.Owner, owner))
            {
                throw new InvalidOperationException(
                    $"[{factory}] on {usage} returned a field owned by another package: pass the owner it receives.");
            }

            return field;
        }
    }
}
