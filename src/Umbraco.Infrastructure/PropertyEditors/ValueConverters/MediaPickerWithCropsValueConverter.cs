using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.ContentApi;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentApi;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ContentApi;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Web.Common.DependencyInjection;
using Umbraco.Extensions;

namespace Umbraco.Cms.Core.PropertyEditors.ValueConverters;

[DefaultPropertyValueConverter]
public class MediaPickerWithCropsValueConverter : PropertyValueConverterBase, IContentApiPropertyValueConverter
{
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IPublishedSnapshotAccessor _publishedSnapshotAccessor;
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IContentNameProvider _contentNameProvider;

    [Obsolete("Use constructor that takes all parameters, scheduled for removal in V14")]
    public MediaPickerWithCropsValueConverter(
        IPublishedSnapshotAccessor publishedSnapshotAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        IJsonSerializer jsonSerializer)
        : this(
            publishedSnapshotAccessor,
            publishedUrlProvider,
            publishedValueFallback,
            jsonSerializer,
            StaticServiceProvider.Instance.GetRequiredService<IContentNameProvider>()
        )
    {
    }

    public MediaPickerWithCropsValueConverter(
        IPublishedSnapshotAccessor publishedSnapshotAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        IJsonSerializer jsonSerializer,
        IContentNameProvider contentNameProvider)
    {
        _publishedSnapshotAccessor = publishedSnapshotAccessor ??
                                     throw new ArgumentNullException(nameof(publishedSnapshotAccessor));
        _publishedUrlProvider = publishedUrlProvider;
        _publishedValueFallback = publishedValueFallback;
        _jsonSerializer = jsonSerializer;
        _contentNameProvider = contentNameProvider;
    }

    public override bool IsConverter(IPublishedPropertyType propertyType) =>
        propertyType.EditorAlias.Equals(Constants.PropertyEditors.Aliases.MediaPicker3);

    public override bool? IsValue(object? value, PropertyValueLevel level)
    {
        var isValue = base.IsValue(value, level);
        if (isValue != false && level == PropertyValueLevel.Source)
        {
            // Empty JSON array is not a value
            isValue = value?.ToString() != "[]";
        }

        return isValue;
    }

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
        => IsMultipleDataType(propertyType.DataType)
            ? typeof(IEnumerable<MediaWithCrops>)
            : typeof(MediaWithCrops);

    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType) =>
        PropertyCacheLevel.Snapshot;

    public override object? ConvertIntermediateToObject(IPublishedElement owner, IPublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object? inter, bool preview)
    {
        var isMultiple = IsMultipleDataType(propertyType.DataType);
        if (string.IsNullOrEmpty(inter?.ToString()))
        {
            // Short-circuit on empty value
            return isMultiple ? Enumerable.Empty<MediaWithCrops>() : null;
        }

        var mediaItems = new List<MediaWithCrops>();
        IEnumerable<MediaPicker3PropertyEditor.MediaPicker3PropertyValueEditor.MediaWithCropsDto> dtos =
            MediaPicker3PropertyEditor.MediaPicker3PropertyValueEditor.Deserialize(_jsonSerializer, inter);
        MediaPicker3Configuration? configuration = propertyType.DataType.ConfigurationAs<MediaPicker3Configuration>();
        IPublishedSnapshot publishedSnapshot = _publishedSnapshotAccessor.GetRequiredPublishedSnapshot();
        foreach (MediaPicker3PropertyEditor.MediaPicker3PropertyValueEditor.MediaWithCropsDto dto in dtos)
        {
            IPublishedContent? mediaItem = publishedSnapshot.Media?.GetById(preview, dto.MediaKey);
            if (mediaItem != null)
            {
                var localCrops = new ImageCropperValue
                {
                    Crops = dto.Crops,
                    FocalPoint = dto.FocalPoint,
                    Src = mediaItem.Url(_publishedUrlProvider),
                };

                localCrops.ApplyConfiguration(configuration);

                // TODO: This should be optimized/cached, as calling Activator.CreateInstance is slow
                Type mediaWithCropsType = typeof(MediaWithCrops<>).MakeGenericType(mediaItem.GetType());
                var mediaWithCrops = (MediaWithCrops)Activator.CreateInstance(mediaWithCropsType, mediaItem, _publishedValueFallback, localCrops)!;

                mediaItems.Add(mediaWithCrops);

                if (!isMultiple)
                {
                    // Short-circuit on single item
                    break;
                }
            }
        }

        return isMultiple ? mediaItems : mediaItems.FirstOrDefault();
    }

    public Type GetContentApiPropertyValueType(IPublishedPropertyType propertyType) => typeof(IEnumerable<ApiMediaWithCrops>);

    public object? ConvertIntermediateToContentApiObject(IPublishedElement owner, IPublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object? inter, bool preview)
    {
        var isMultiple = IsMultipleDataType(propertyType.DataType);

        ApiMediaWithCrops ToApiMedia(MediaWithCrops media)
        {
            var customProperties = media
                .Properties
                .Where(p => p.Alias.StartsWith("umbraco") == false)
                .ToDictionary(p => p.Alias, p => p.GetContentApiValue());

            return new ApiMediaWithCrops(
                media.Content.Key,
                _contentNameProvider.GetName(media.Content),
                media.ContentType.Alias,
                media.LocalCrops.Src ?? string.Empty,
                media.Value<string>(_publishedValueFallback, Constants.Conventions.Media.Extension),
                media.Value<int?>(_publishedValueFallback, Constants.Conventions.Media.Width),
                media.Value<int?>(_publishedValueFallback, Constants.Conventions.Media.Height),
                customProperties)
            {
                FocalPoint = media.LocalCrops.FocalPoint,
                Crops = media.LocalCrops.Crops
            };
        }

        // NOTE: eventually we might implement this explicitly instead of piggybacking on the default object conversion. however, this only happens once per cache rebuild,
        // and the performance gain from an explicit implementation is negligible, so... at least for the time being this will do just fine.
        var converted = ConvertIntermediateToObject(owner, propertyType, referenceCacheLevel, inter, preview);
        if (isMultiple && converted is IEnumerable<MediaWithCrops> mediasWithCrops)
        {
            return mediasWithCrops.Select(ToApiMedia).ToArray();
        }
        if (isMultiple == false && converted is MediaWithCrops mediaWithCrops)
        {
            return new [] { ToApiMedia(mediaWithCrops) };
        }

        return Array.Empty<ApiMediaWithCrops>();
    }

    private bool IsMultipleDataType(PublishedDataType dataType) =>
        dataType.ConfigurationAs<MediaPicker3Configuration>()?.Multiple ?? false;
}
