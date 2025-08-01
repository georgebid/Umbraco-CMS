// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Builders;

namespace Umbraco.Cms.Tests.Integration.Testing;

public abstract class UmbracoIntegrationTestWithContent : UmbracoIntegrationTest
{
    protected const string TextpageKey = "B58B3AD4-62C2-4E27-B1BE-837BD7C533E0";
    protected const string SubPageKey = "07EABF4A-5C62-4662-9F2A-15BBB488BCA5";
    protected const string SubPage2Key = "0EED78FC-A6A8-4587-AB18-D3AFE212B1C4";
    protected const string SubPage3Key = "29BBB8CF-E69B-4A21-9363-02ED5B6637C4";
    protected const string TrashedKey = "EAE9EE57-FFE4-4841-8586-1B636C43A3D4";

    protected IContentTypeService ContentTypeService => GetRequiredService<IContentTypeService>();

    protected IDataTypeService DataTypeService => GetRequiredService<IDataTypeService>();

    protected IFileService FileService => GetRequiredService<IFileService>();

    protected ContentService ContentService => (ContentService)GetRequiredService<IContentService>();

    protected Content Trashed { get; private set; }

    protected Content Subpage2 { get; private set; }
    protected Content Subpage3 { get; private set; }

    protected Content Subpage { get; private set; }

    protected Content Textpage { get; private set; }

    protected ContentType ContentType { get; private set; }

    [SetUp]
    public virtual void Setup() => CreateTestData();

    public virtual void CreateTestData()
    {
        // NOTE Maybe not the best way to create/save test data as we are using the services, which are being tested.
        var template = TemplateBuilder.CreateTextPageTemplate("defaultTemplate");
        FileService.SaveTemplate(template);

        // Create and Save ContentType "umbTextpage" -> 1051 (template), 1052 (content type)
        ContentType =
            ContentTypeBuilder.CreateSimpleContentType("umbTextpage", "Textpage", defaultTemplateId: template.Id);
        ContentType.Key = new Guid("1D3A8E6E-2EA9-4CC1-B229-1AEE19821522");
        ContentTypeService.Save(ContentType);

        // Create and Save Content "Homepage" based on "umbTextpage" -> 1053
        Textpage = ContentBuilder.CreateSimpleContent(ContentType, "Textpage");
        Textpage.Key = new Guid(TextpageKey);
        ContentService.Save(Textpage, -1);

        // Create and Save Content "Text Page 1" based on "umbTextpage" -> 1054
        Subpage = ContentBuilder.CreateSimpleContent(ContentType, "Text Page 1", Textpage.Id);
        Subpage.Key = new Guid(SubPageKey);
        var contentSchedule = ContentScheduleCollection.CreateWithEntry(DateTime.UtcNow.AddMinutes(-5), null);
        ContentService.Save(Subpage, -1, contentSchedule);

        // Create and Save Content "Text Page 1" based on "umbTextpage" -> 1055
        Subpage2 = ContentBuilder.CreateSimpleContent(ContentType, "Text Page 2", Textpage.Id);
        Subpage2.Key = new Guid(SubPage2Key);
        ContentService.Save(Subpage2, -1);


        Subpage3 = ContentBuilder.CreateSimpleContent(ContentType, "Text Page 3", Textpage.Id);
        Subpage3.Key = new Guid(SubPage3Key);
        ContentService.Save(Subpage3, -1);

        // Create and Save Content "Text Page Deleted" based on "umbTextpage" -> 1056
        Trashed = ContentBuilder.CreateSimpleContent(ContentType, "Text Page Deleted", -20);
        Trashed.Trashed = true;
        Trashed.Key = new Guid(TrashedKey);
        ContentService.Save(Trashed, -1);
    }
}
