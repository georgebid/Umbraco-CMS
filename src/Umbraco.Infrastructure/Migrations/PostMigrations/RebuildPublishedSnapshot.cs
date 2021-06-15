﻿using Umbraco.Cms.Core.Migrations;

namespace Umbraco.Cms.Infrastructure.Migrations.PostMigrations
{
    /// <summary>
    /// Rebuilds the published snapshot.
    /// </summary>
    public class RebuildPublishedSnapshot : IMigration
    {
        private readonly IPublishedSnapshotRebuilder _rebuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="RebuildPublishedSnapshot"/> class.
        /// </summary>
        public RebuildPublishedSnapshot(IMigrationContext context, IPublishedSnapshotRebuilder rebuilder)
        {
            _rebuilder = rebuilder;
        }

        /// <inheritdoc />
        public void Migrate()
        {
            _rebuilder.Rebuild();
        }
    }
}