using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseModelDefinitions;

namespace Umbraco.Cms.Infrastructure.Persistence.Dtos;

[TableName(TableName)]
[PrimaryKey("id")]
[ExplicitColumns]
internal sealed class LogDto
{
    public const string TableName = Constants.DatabaseSchema.Tables.Log;

    private int? _userId;

    [Column("id")]
    [PrimaryKeyColumn]
    public int Id { get; set; }

    [Column("userId")]
    [ForeignKey(typeof(UserDto))]
    [NullSetting(NullSetting = NullSettings.Null)]
    public int? UserId { get => _userId == 0 ? null : _userId; set => _userId = value; } // return null if zero

    [Column("NodeId")]
    [Index(IndexTypes.NonClustered, Name = "IX_umbracoLog")]
    public int NodeId { get; set; }

    /// <summary>
    ///     This is the entity type associated with the log
    /// </summary>
    [Column("entityType")]
    [Length(50)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? EntityType { get; set; }

    [Column("Datestamp", ForceToUtc = false)]
    [Constraint(Default = SystemMethods.CurrentDateTime)]
    [Index(IndexTypes.NonClustered, Name = "IX_" + TableName + "_datestamp", ForColumns = "Datestamp,userId,NodeId")]
    public DateTime Datestamp { get; set; }

    [Column("logHeader")]
    [Length(50)]
    [Index(IndexTypes.NonClustered, Name = "IX_" + TableName + "_datestamp_logheader", ForColumns = "Datestamp,logHeader")]
    public string Header { get; set; } = null!;

    [Column("logComment")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [Length(4000)]
    public string? Comment { get; set; }

    /// <summary>
    ///     Used to store additional data parameters for the log
    /// </summary>
    [Column("parameters")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [Length(4000)]
    public string? Parameters { get; set; }
}
