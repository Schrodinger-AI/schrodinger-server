using System;
using Volo.Abp.Domain.Entities;

namespace SchrodingerServer.Entities
{
    /// <inheritdoc cref="IEntity" />
    [Serializable]
    public abstract class SchrodingerEntity<TKey> : Entity, IEntity<TKey>
    {
        /// <inheritdoc/>
        public virtual TKey Id { get; set; }

        protected SchrodingerEntity()
        {

        }

        protected SchrodingerEntity(TKey id)
        {
            Id = id;
        }

        public override object[] GetKeys()
        {
            return new object[] {Id};
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"[ENTITY: {GetType().Name}] Id = {Id}";
        }
    }
}