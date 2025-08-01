using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DeliveryApi;
using Umbraco.Cms.Core.DeliveryApi.Accessors;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.DeliveryApi;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Tests.Common;
using Umbraco.Extensions;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Core.DeliveryApi;

public class DeliveryApiTests
{
    protected IPublishedPropertyType DeliveryApiPropertyType { get; private set; }

    protected IPublishedPropertyType DefaultPropertyType { get; private set; }

    protected IPublishStatusQueryService PublishStatusQueryService { get; private set; }

    [SetUp]
    public virtual void Setup()
    {
        var deliveryApiPropertyValueConverter = new Mock<IDeliveryApiPropertyValueConverter>();
        deliveryApiPropertyValueConverter.Setup(p => p.ConvertIntermediateToDeliveryApiObject(
            It.IsAny<IPublishedElement>(),
            It.IsAny<IPublishedPropertyType>(),
            It.IsAny<PropertyCacheLevel>(),
            It.IsAny<object?>(),
            It.IsAny<bool>(),
            It.IsAny<bool>())).Returns("Delivery API value");
        deliveryApiPropertyValueConverter.Setup(p => p.ConvertIntermediateToObject(
            It.IsAny<IPublishedElement>(),
            It.IsAny<IPublishedPropertyType>(),
            It.IsAny<PropertyCacheLevel>(),
            It.IsAny<object?>(),
            It.IsAny<bool>())).Returns("Default value");
        deliveryApiPropertyValueConverter.Setup(p => p.IsConverter(It.IsAny<IPublishedPropertyType>())).Returns(true);
        deliveryApiPropertyValueConverter.Setup(p => p.IsValue(It.IsAny<object?>(), It.IsAny<PropertyValueLevel>())).Returns(true);
        deliveryApiPropertyValueConverter.Setup(p => p.GetPropertyCacheLevel(It.IsAny<IPublishedPropertyType>())).Returns(PropertyCacheLevel.None);
        deliveryApiPropertyValueConverter.Setup(p => p.GetDeliveryApiPropertyCacheLevel(It.IsAny<IPublishedPropertyType>())).Returns(PropertyCacheLevel.None);
        deliveryApiPropertyValueConverter.Setup(p => p.GetDeliveryApiPropertyCacheLevelForExpansion(It.IsAny<IPublishedPropertyType>())).Returns(PropertyCacheLevel.None);

        DeliveryApiPropertyType = SetupPublishedPropertyType(deliveryApiPropertyValueConverter.Object, "deliveryApi", "Delivery.Api.Editor");

        var defaultPropertyValueConverter = new Mock<IPropertyValueConverter>();
        defaultPropertyValueConverter.Setup(p => p.ConvertIntermediateToObject(
            It.IsAny<IPublishedElement>(),
            It.IsAny<IPublishedPropertyType>(),
            It.IsAny<PropertyCacheLevel>(),
            It.IsAny<object?>(),
            It.IsAny<bool>())).Returns("Default value");
        defaultPropertyValueConverter.Setup(p => p.IsConverter(It.IsAny<IPublishedPropertyType>())).Returns(true);
        defaultPropertyValueConverter.Setup(p => p.IsValue(It.IsAny<object?>(), It.IsAny<PropertyValueLevel>())).Returns(true);
        defaultPropertyValueConverter.Setup(p => p.GetPropertyCacheLevel(It.IsAny<IPublishedPropertyType>())).Returns(PropertyCacheLevel.None);

        DefaultPropertyType = SetupPublishedPropertyType(defaultPropertyValueConverter.Object, "default", "Default.Editor");

        var publishStatusQueryService = new Mock<IPublishStatusQueryService>();
        publishStatusQueryService
            .Setup(x => x.IsDocumentPublished(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(true);
        publishStatusQueryService
            .Setup(x => x.HasPublishedAncestorPath(It.IsAny<Guid>()))
            .Returns(true);

        PublishStatusQueryService = publishStatusQueryService.Object;
    }

    protected IPublishedPropertyType SetupPublishedPropertyType(
        IPropertyValueConverter valueConverter,
        string propertyTypeAlias,
        string editorAlias,
        object? dataTypeConfiguration = null,
        ContentVariation contentVariation = ContentVariation.Nothing)
    {
        var mockPublishedContentTypeFactory = new Mock<IPublishedContentTypeFactory>();
        mockPublishedContentTypeFactory.Setup(x => x.GetDataType(It.IsAny<int>()))
            .Returns(new PublishedDataType(123, editorAlias, editorAlias, new Lazy<object>(() => dataTypeConfiguration)));

        var publishedPropType = new PublishedPropertyType(
            propertyTypeAlias,
            123,
            true,
            contentVariation,
            new PropertyValueConverterCollection(() => new[] { valueConverter }),
            Mock.Of<IPublishedModelFactory>(),
            mockPublishedContentTypeFactory.Object);

        return publishedPropType;
    }

    protected IOutputExpansionStrategyAccessor CreateOutputExpansionStrategyAccessor() => new NoopOutputExpansionStrategyAccessor();

    protected IVariationContextAccessor CreateVariationContextAccessor() => new TestVariationContextAccessor();

    protected IOptions<GlobalSettings> CreateGlobalSettings(bool hideTopLevelNodeFromPath = true)
    {
        var globalSettings = new GlobalSettings { HideTopLevelNodeFromPath = hideTopLevelNodeFromPath };
        var globalSettingsOptionsMock = new Mock<IOptions<GlobalSettings>>();
        globalSettingsOptionsMock.SetupGet(s => s.Value).Returns(globalSettings);
        return globalSettingsOptionsMock.Object;
    }

    protected void ConfigurePublishedContentMock(Mock<IPublishedContent> content, Guid key, string name, string urlSegment, IPublishedContentType contentType, IEnumerable<IPublishedProperty> properties)
    {
        content.SetupGet(c => c.Key).Returns(key);
        content.SetupGet(c => c.Name).Returns(name);
        content.SetupGet(c => c.UrlSegment).Returns(urlSegment);
        content
            .SetupGet(m => m.Cultures)
            .Returns(new Dictionary<string, PublishedCultureInfo>()
            {
                {
                    string.Empty,
                    new PublishedCultureInfo(string.Empty, name, urlSegment, DateTime.Now)
                }
            });
        content.SetupGet(c => c.ContentType).Returns(contentType);
        content.SetupGet(c => c.Properties).Returns(properties);
        content.SetupGet(c => c.ItemType).Returns(contentType.ItemType);
        content.SetupGet(c => c.Level).Returns(1);
        content.Setup(c => c.IsPublished(It.IsAny<string?>())).Returns(true);
    }

    protected string DefaultUrlSegment(string name, string? culture = null)
        => $"{name.ToLowerInvariant().Replace(" ", "-")}{(culture.IsNullOrWhiteSpace() ? string.Empty : $"-{culture}")}";

    protected virtual ApiContentRouteBuilder CreateContentRouteBuilder(
        IApiContentPathProvider contentPathProvider,
        IOptions<GlobalSettings> globalSettings,
        IVariationContextAccessor? variationContextAccessor = null,
        IRequestPreviewService? requestPreviewService = null,
        IOptionsMonitor<RequestHandlerSettings>? requestHandlerSettingsMonitor = null,
        IPublishedContentCache? contentCache = null,
        IDocumentNavigationQueryService? navigationQueryService = null,
        IPublishStatusQueryService? publishStatusQueryService = null,
        IDocumentUrlService? documentUrlService = null)
    {
        if (requestHandlerSettingsMonitor == null)
        {
            var mock = new Mock<IOptionsMonitor<RequestHandlerSettings>>();
            mock.SetupGet(m => m.CurrentValue).Returns(new RequestHandlerSettings());
            requestHandlerSettingsMonitor = mock.Object;
        }

        return new ApiContentRouteBuilder(
            contentPathProvider,
            globalSettings,
            variationContextAccessor ?? Mock.Of<IVariationContextAccessor>(),
            requestPreviewService ?? Mock.Of<IRequestPreviewService>(),
            requestHandlerSettingsMonitor,
            contentCache ?? Mock.Of<IPublishedContentCache>(),
            navigationQueryService ?? Mock.Of<IDocumentNavigationQueryService>(),
            publishStatusQueryService ?? PublishStatusQueryService,
            documentUrlService ?? Mock.Of<IDocumentUrlService>());
    }
}
