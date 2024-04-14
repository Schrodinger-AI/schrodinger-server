using Volo.Abp.Domain.Entities;

namespace SchrodingerServer.Entities;

public abstract class AbstractEntity<TKey> : Entity, IEntity<TKey>
{
    /// <inheritdoc/>
    public virtual TKey Id { get; set; }

    protected AbstractEntity()
    {
    }

    protected AbstractEntity(TKey id)
    {
        Id = id;
    }

    public override object[] GetKeys()
    {
        return new object[] { Id };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[ENTITY: {GetType().Name}] Id = {Id}";
    }
}