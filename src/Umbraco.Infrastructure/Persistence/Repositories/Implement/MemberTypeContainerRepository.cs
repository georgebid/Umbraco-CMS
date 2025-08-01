using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Repositories;

namespace Umbraco.Cms.Infrastructure.Persistence.Repositories.Implement
{
    /// <summary>
    /// A no-op implementation of <see cref="IMemberTypeContainerRepository"/>, as containers aren't supported for members.
    /// </summary>
    /// <remarks>
    /// Introduced to avoid inconsistencies with nullability of dependencies for type repositories for content, media and members.
    /// </remarks>
    internal sealed class MemberTypeContainerRepository : IMemberTypeContainerRepository
    {
        public void Delete(EntityContainer entity)
        {
        }

        public bool Exists(int id) => false;

        public EntityContainer? Get(Guid id) => null;

        public IEnumerable<EntityContainer> Get(string name, int level) => Enumerable.Empty<EntityContainer>();

        public bool HasDuplicateName(Guid parentKey, string name) => false;

        public bool HasDuplicateName(int parentId, string name) => false;

        public EntityContainer? Get(int id) => null;

        public IEnumerable<EntityContainer> GetMany(params int[]? ids) => Enumerable.Empty<EntityContainer>();

        public void Save(EntityContainer entity)
        {
        }
    }
}
