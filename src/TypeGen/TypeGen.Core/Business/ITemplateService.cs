﻿namespace TypeGen.Core.Business
{
    internal interface ITemplateService
    {
        string FillClassTemplate(string imports, string name, string extends, string properties, string customHead, string customBody, string fileHeading = null);
        string FillClassDefaultExportTemplate(string imports, string name, string exportName, string extends, string properties, string customHead, string customBody, string fileHeading = null);
        string FillClassPropertyTemplate(string modifiers, string name, string type, string defaultValue = null);
        string FillInterfaceTemplate(string imports, string name, string extends, string properties, string customHead, string customBody, string fileHeading = null);
        string FillInterfaceDefaultExportTemplate(string imports, string name, string exportName, string extends, string properties, string customHead, string customBody, string fileHeading = null);
        string FillInterfacePropertyTemplate(string modifiers, string name, string type, bool isOptional);
        string FillEnumTemplate(string imports, string name, string values, bool isConst, string fileHeading = null);
        string FillEnumDefaultExportTemplate(string imports, string name, string values, bool isConst, string fileHeading = null);
        string FillEnumValueTemplate(string name, object value);
        string FillImportTemplate(string name, string typeAlias, string path);
        string FillImportDefaultExportTemplate(string name, string path);
        string FillIndexTemplate(string exports);
        string FillIndexExportTemplate(string filename);
        string GetExtendsText(string name);
    }
}