// Copyright (c) Umbraco.
// See LICENSE for more details.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Cache.PropertyEditors;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.PropertyEditors.Validators;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Templates;
using Umbraco.Cms.Infrastructure.Extensions;
using Umbraco.Extensions;

namespace Umbraco.Cms.Core.PropertyEditors;

/// <summary>
///     Represents a rich text property editor.
/// </summary>
[DataEditor(
    Constants.PropertyEditors.Aliases.RichText,
    ValueType = ValueTypes.Text,
    ValueEditorIsReusable = true)]
public class RichTextPropertyEditor : DataEditor
{
    private readonly IIOHelper _ioHelper;
    private readonly IRichTextPropertyIndexValueFactory _richTextPropertyIndexValueFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RichTextPropertyEditor"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor will setup the property editor based on the attribute if one is found.
    /// </remarks>
    public RichTextPropertyEditor(
        IDataValueEditorFactory dataValueEditorFactory,
        IIOHelper ioHelper,
        IRichTextPropertyIndexValueFactory richTextPropertyIndexValueFactory)
        : base(dataValueEditorFactory)
    {
        _ioHelper = ioHelper;
        _richTextPropertyIndexValueFactory = richTextPropertyIndexValueFactory;

        SupportsReadOnly = true;
    }

    public override IPropertyIndexValueFactory PropertyIndexValueFactory => _richTextPropertyIndexValueFactory;

    public override bool SupportsConfigurableElements => true;

    /// <inheritdoc />
    public override bool CanMergePartialPropertyValues(IPropertyType propertyType) => propertyType.VariesByCulture() is false;

    /// <inheritdoc />
    public override object? MergePartialPropertyValueForCulture(object? sourceValue, object? targetValue, string? culture)
    {
        var valueEditor = (RichTextPropertyValueEditor)GetValueEditor();
        return valueEditor.MergePartialPropertyValueForCulture(sourceValue, targetValue, culture);
    }

    public override object? MergeVariantInvariantPropertyValue(
        object? sourceValue,
        object? targetValue,
        bool canUpdateInvariantData,
        HashSet<string> allowedCultures)
    {
        var valueEditor = (RichTextPropertyValueEditor)GetValueEditor();
        return valueEditor.MergeVariantInvariantPropertyValue(sourceValue, targetValue, canUpdateInvariantData,allowedCultures);
    }

    /// <summary>
    ///     Create a custom value editor
    /// </summary>
    /// <returns></returns>
    protected override IDataValueEditor CreateValueEditor() =>
        DataValueEditorFactory.Create<RichTextPropertyValueEditor>(Attribute!);

    protected override IConfigurationEditor CreateConfigurationEditor() =>
        new RichTextConfigurationEditor(_ioHelper);

    /// <summary>
    ///     A custom value editor to ensure that images and blocks are parsed when being persisted and formatted correctly for
    ///     display in the editor
    /// </summary>
    internal sealed class RichTextPropertyValueEditor : BlockValuePropertyValueEditorBase<RichTextBlockValue, RichTextBlockLayoutItem>
    {
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly IHtmlSanitizer _htmlSanitizer;
        private readonly HtmlImageSourceParser _imageSourceParser;
        private readonly HtmlLocalLinkParser _localLinkParser;
        private readonly RichTextEditorPastedImages _pastedImages;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IRichTextRequiredValidator _richTextRequiredValidator;
        private readonly IRichTextRegexValidator _richTextRegexValidator;
        private readonly ILogger<RichTextPropertyValueEditor> _logger;
        private readonly IBlockEditorElementTypeCache _elementTypeCache;

        public RichTextPropertyValueEditor(
            DataEditorAttribute attribute,
            PropertyEditorCollection propertyEditors,
            IDataTypeConfigurationCache dataTypeReadCache,
            ILogger<RichTextPropertyValueEditor> logger,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
            IShortStringHelper shortStringHelper,
            HtmlImageSourceParser imageSourceParser,
            HtmlLocalLinkParser localLinkParser,
            RichTextEditorPastedImages pastedImages,
            IJsonSerializer jsonSerializer,
            IHtmlSanitizer htmlSanitizer,
            IBlockEditorElementTypeCache elementTypeCache,
            IPropertyValidationService propertyValidationService,
            DataValueReferenceFactoryCollection dataValueReferenceFactoryCollection,
            IRichTextRequiredValidator richTextRequiredValidator,
            IRichTextRegexValidator richTextRegexValidator,
            BlockEditorVarianceHandler blockEditorVarianceHandler,
            ILanguageService languageService,
            IIOHelper ioHelper)
            : base(propertyEditors, dataTypeReadCache, shortStringHelper, jsonSerializer, dataValueReferenceFactoryCollection, blockEditorVarianceHandler, languageService, ioHelper, attribute)
        {
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _imageSourceParser = imageSourceParser;
            _localLinkParser = localLinkParser;
            _pastedImages = pastedImages;
            _htmlSanitizer = htmlSanitizer;
            _elementTypeCache = elementTypeCache;
            _richTextRequiredValidator = richTextRequiredValidator;
            _richTextRegexValidator = richTextRegexValidator;
            _jsonSerializer = jsonSerializer;
            _logger = logger;

            BlockEditorValues = new(new RichTextEditorBlockDataConverter(_jsonSerializer), elementTypeCache, logger);
            Validators.Add(new RichTextEditorBlockValidator(propertyValidationService, BlockEditorValues, elementTypeCache, jsonSerializer, logger));
        }

        public override IValueRequiredValidator RequiredValidator => _richTextRequiredValidator;

        public override IValueFormatValidator FormatValidator => _richTextRegexValidator;

        protected override RichTextBlockValue CreateWithLayout(IEnumerable<RichTextBlockLayoutItem> layout) => new(layout);

        /// <inheritdoc />
        public override object? ConfigurationObject
        {
            get => base.ConfigurationObject;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!(value is RichTextConfiguration configuration))
                {
                    throw new ArgumentException(
                        $"Expected a {typeof(RichTextConfiguration).Name} instance, but got {value.GetType().Name}.",
                        nameof(value));
                }

                base.ConfigurationObject = value;
            }
        }

        /// <summary>
        ///     Resolve references from <see cref="IDataValueEditor" /> values
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public override IEnumerable<UmbracoEntityReference> GetReferences(object? value)
        {
            if (TryParseEditorValue(value, out RichTextEditorValue? richTextEditorValue) is false)
            {
                return Array.Empty<UmbracoEntityReference>();
            }

            var references = new List<UmbracoEntityReference>();

            // image references from markup
            references.AddRange(_imageSourceParser
                .FindUdisFromDataAttributes(richTextEditorValue.Markup)
                .Select(udi => new UmbracoEntityReference(udi)));

            // local link references from markup
            references.AddRange(_localLinkParser
                .FindUdisFromLocalLinks(richTextEditorValue.Markup)
                .WhereNotNull()
                .Select(udi => new UmbracoEntityReference(udi)));

            // references from blocksIg
            if (richTextEditorValue.Blocks is not null)
            {
                BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>? blockEditorData = ConvertAndClean(richTextEditorValue.Blocks);
                if (blockEditorData is not null)
                {
                    references.AddRange(GetBlockValueReferences(blockEditorData.BlockValue));
                }
            }

            return references;
        }

        public override IEnumerable<ITag> GetTags(object? value, object? dataTypeConfiguration, int? languageId)
        {
            if (TryParseEditorValue(value, out RichTextEditorValue? richTextEditorValue) is false || richTextEditorValue.Blocks is null)
            {
                return Array.Empty<ITag>();
            }

            BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>? blockEditorData = ConvertAndClean(richTextEditorValue.Blocks);
            if (blockEditorData is null)
            {
                return Array.Empty<ITag>();
            }

            return GetBlockValueTags(blockEditorData.BlockValue, languageId);
        }

        /// <summary>
        ///     Format the data for the editor
        /// </summary>
        /// <param name="property"></param>
        /// <param name="culture"></param>
        /// <param name="segment"></param>
        public override object? ToEditor(IProperty property, string? culture = null, string? segment = null)
        {
            var value = property.GetValue(culture, segment);
            if (TryParseEditorValue(value, out RichTextEditorValue? richTextEditorValue) is false)
            {
                return null;
            }

            richTextEditorValue.Markup = _imageSourceParser.EnsureImageSources(richTextEditorValue.Markup);

            // return json convertable object
            return CleanAndMapBlocks(richTextEditorValue, blockValue => MapBlockValueToEditor(property, blockValue, culture, segment));
        }

        /// <summary>
        ///     Format the data for persistence
        /// </summary>
        /// <param name="editorValue"></param>
        /// <param name="currentValue"></param>
        /// <returns></returns>
        public override object? FromEditor(ContentPropertyData editorValue, object? currentValue)
        {
            // See note on BlockEditorPropertyValueEditor.FromEditor for why we can't return early with only a null or empty editorValue.
            TryParseEditorValue(editorValue.Value, out RichTextEditorValue? richTextEditorValue);
            TryParseEditorValue(currentValue, out RichTextEditorValue? currentRichTextEditorValue);

            // We can early return if we have a null value and the current value doesn't have any blocks.
            if (richTextEditorValue is null && currentRichTextEditorValue?.Blocks is null)
            {
                return null;
            }

            // Ensure the property type is populated on all blocks.
            richTextEditorValue?.EnsurePropertyTypePopulatedOnBlocks(_elementTypeCache);
            currentRichTextEditorValue?.EnsurePropertyTypePopulatedOnBlocks(_elementTypeCache);

            RichTextEditorValue cleanedUpRichTextEditorValue = CleanAndMapBlocks(richTextEditorValue, blockValue => MapBlockValueFromEditor(blockValue, currentRichTextEditorValue?.Blocks, editorValue.ContentKey));

            if (string.IsNullOrWhiteSpace(richTextEditorValue?.Markup))
            {
                return null;
            }

            Guid userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key ??
                          Constants.Security.SuperUserKey;

            var config = editorValue.DataTypeConfiguration as RichTextConfiguration;
            Guid mediaParentId = config?.MediaParentId ?? Guid.Empty;

            var parseAndSavedTempImages = _pastedImages
                .FindAndPersistPastedTempImagesAsync(richTextEditorValue.Markup, mediaParentId, userKey)
                .GetAwaiter()
                .GetResult();
            var editorValueWithMediaUrlsRemoved = _imageSourceParser.RemoveImageSources(parseAndSavedTempImages);
            var sanitized = _htmlSanitizer.Sanitize(editorValueWithMediaUrlsRemoved);

            richTextEditorValue.Markup = sanitized.NullOrWhiteSpaceAsNull() ?? string.Empty;

            // return json
            return RichTextPropertyEditorHelper.SerializeRichTextEditorValue(cleanedUpRichTextEditorValue, _jsonSerializer);
        }

        public override IEnumerable<Guid> ConfiguredElementTypeKeys()
        {
            var configuration = ConfigurationObject as RichTextConfiguration;
            return configuration?.Blocks?.SelectMany(ConfiguredElementTypeKeys) ?? Enumerable.Empty<Guid>();
        }

        internal override object? MergeVariantInvariantPropertyValue(
            object? sourceValue,
            object? targetValue,
            bool canUpdateInvariantData,
            HashSet<string> allowedCultures)
        {
            TryParseEditorValue(sourceValue, out RichTextEditorValue? sourceRichTextEditorValue);
            TryParseEditorValue(targetValue, out RichTextEditorValue? targetRichTextEditorValue);

            var mergedBlockValue = MergeBlockVariantInvariantData(
                sourceRichTextEditorValue?.Blocks,
                targetRichTextEditorValue?.Blocks,
                canUpdateInvariantData,
                allowedCultures);

            var mergedMarkupValue = MergeMarkupValue(
                sourceRichTextEditorValue?.Markup ?? string.Empty,
                targetRichTextEditorValue?.Markup ?? string.Empty,
                mergedBlockValue,
                canUpdateInvariantData);

            var mergedEditorValue = new RichTextEditorValue { Markup = mergedMarkupValue, Blocks = mergedBlockValue };
            return RichTextPropertyEditorHelper.SerializeRichTextEditorValue(mergedEditorValue, _jsonSerializer);
        }

        private static string MergeMarkupValue(
            string source,
            string target,
            RichTextBlockValue? mergedBlockValue,
            bool canUpdateInvariantData)
        {
            // pick source or target based on culture permissions
            var mergedMarkup = canUpdateInvariantData ? target : source;

            // todo? strip all invalid block links from markup, those tat are no longer in the layout
            return mergedMarkup;
        }

        private RichTextBlockValue? MergeBlockVariantInvariantData(
            RichTextBlockValue? sourceRichTextBlockValue,
            RichTextBlockValue? targetRichTextBlockValue,
            bool canUpdateInvariantData,
            HashSet<string> allowedCultures)
        {
            if (sourceRichTextBlockValue is null && targetRichTextBlockValue is null)
            {
                return null;
            }

            BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem> sourceBlockEditorData =
                (sourceRichTextBlockValue is not null ? ConvertAndClean(sourceRichTextBlockValue) : null)
                ?? new BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>([], new RichTextBlockValue());

            BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem> targetBlockEditorData =
                (targetRichTextBlockValue is not null ? ConvertAndClean(targetRichTextBlockValue) : null)
                ?? new BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>([], new RichTextBlockValue());

            return MergeVariantInvariantPropertyValueTyped(
                sourceBlockEditorData,
                targetBlockEditorData,
                canUpdateInvariantData,
                allowedCultures);
        }

        internal override object? MergePartialPropertyValueForCulture(object? sourceValue, object? targetValue, string? culture)
        {
            if (sourceValue is null || TryParseEditorValue(sourceValue, out RichTextEditorValue? sourceRichTextEditorValue) is false)
            {
                return null;
            }

            if (sourceRichTextEditorValue.Blocks is null)
            {
                return sourceValue;
            }

            BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>? sourceBlockEditorData = ConvertAndClean(sourceRichTextEditorValue.Blocks);
            if (sourceBlockEditorData?.Layout is null)
            {
                return sourceValue;
            }

            TryParseEditorValue(targetValue, out RichTextEditorValue? targetRichTextEditorValue);

            BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem> targetBlockEditorData =
                (targetRichTextEditorValue?.Blocks is not null ? ConvertAndClean(targetRichTextEditorValue.Blocks) : null)
                ?? new BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>([], CreateWithLayout(sourceBlockEditorData.Layout));

            RichTextBlockValue blocksMergeResult = MergeBlockEditorDataForCulture(sourceBlockEditorData.BlockValue, targetBlockEditorData.BlockValue, culture);

            // structure is global, and markup follows structure
            var mergedEditorValue = new RichTextEditorValue { Markup = sourceRichTextEditorValue.Markup, Blocks = blocksMergeResult };
            return RichTextPropertyEditorHelper.SerializeRichTextEditorValue(mergedEditorValue, _jsonSerializer);
        }

        private bool TryParseEditorValue(object? value, [NotNullWhen(true)] out RichTextEditorValue? richTextEditorValue)
            => RichTextPropertyEditorHelper.TryParseRichTextEditorValue(value, _jsonSerializer, _logger, out richTextEditorValue);

        private RichTextEditorValue CleanAndMapBlocks(RichTextEditorValue? richTextEditorValue, Action<RichTextBlockValue> handleMapping)
        {
            // We handle mapping of blocks even if the edited value is empty, so property editors can clean up any resources
            // relating to removed blocks, e.g. files uploaded to the media library from the file upload property editor.
            BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>? blockEditorData = null;
            if (richTextEditorValue?.Blocks is not null)
            {
                blockEditorData = ConvertAndClean(richTextEditorValue.Blocks);
            }

            handleMapping(blockEditorData?.BlockValue ?? new RichTextBlockValue());

            if (richTextEditorValue?.Blocks is null)
            {
                // no blocks defined, store empty block value
                return MarkupWithEmptyBlocks();
            }

            if (blockEditorData is not null)
            {
                return new RichTextEditorValue
                {
                    Markup = richTextEditorValue.Markup,
                    Blocks = blockEditorData.BlockValue,
                };
            }

            // could not deserialize the blocks or handle the mapping, store empty block value
            return MarkupWithEmptyBlocks();

            RichTextEditorValue MarkupWithEmptyBlocks() => new()
            {
                Markup = richTextEditorValue?.Markup ?? string.Empty,
                Blocks = new RichTextBlockValue(),
            };
        }

        private BlockEditorData<RichTextBlockValue, RichTextBlockLayoutItem>? ConvertAndClean(RichTextBlockValue blockValue)
            => BlockEditorValues.ConvertAndClean(blockValue);
    }
}
