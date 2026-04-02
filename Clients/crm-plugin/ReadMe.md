# CRM Plugin -- PDF Generation API Integration

## Overview

A Dynamics 365 CRM v9.x plugin that generates PDF reports by sending HTML templates to the DocGen PDF API. The plugin loads an HTML template from a custom CRM entity, binds Case field values into placeholders, calls the API, and stores the resulting PDF as a Note (annotation) on the Case.

## Integration Flow

```
Case record updated
       |
       v
Plugin reads new_reporttype from Case
       |
       v
Queries new_pdftemplate entity for matching template
       |
       v
Replaces {{placeholder}} tokens with Case field values
       |
       v
POST /api/pdf/generateBase64  -->  DocGen PDF API
       |
       v
Receives JSON { fileName, contentType, base64 }
       |
       v
Saves base64 PDF as annotation (Note) on Case
       |
       v
Updates Case statuscode (Completed / Resubmit)
```

## CRM Entities

### Case (incident) -- built-in entity

The standard Case entity is used as the request record. A custom field is added to identify which report template to use.

| Field | Type | Description |
|---|---|---|
| `new_reporttype` | Single Line of Text | Report type key that maps to a template record (e.g. `permit_noc`, `approval_notification`, `technical_report`) |

The plugin also reads all other Case fields at runtime to bind them into the HTML template placeholders.

#### Custom Status Reason Values

Two custom `statuscode` option-set values must be added to the Case entity:

| Label | Value | Used When |
|---|---|---|
| Completed | `100000001` | Report type is `permit_noc` |
| Resubmit | `100000002` | Report type is `approval_notification` or `technical_report` |

> Update the constants `StatusCompleted` and `StatusResubmit` in `PdfGenerationPlugin.cs` if your environment uses different option-set values.

### PDF Template (new_pdftemplate) -- custom entity

A custom entity that stores HTML templates for each report type. Create one record per report type.

| Field | Logical Name | Type | Description |
|---|---|---|---|
| Template Key | `new_templatekey` | Single Line of Text | Unique key matching the Case `new_reporttype` value (e.g. `permit_noc`) |
| Body HTML | `new_bodyhtmlcontent` | Multiple Lines of Text | The main report body HTML. Supports `{{fieldname}}` placeholders |
| Header HTML | `new_headerhtmlcontent` | Multiple Lines of Text | HTML rendered as a repeating header on every PDF page |
| Footer HTML | `new_footerhtmlcontent` | Multiple Lines of Text | HTML rendered as a repeating footer on every PDF page |

### Annotation (annotation) -- built-in entity

The plugin creates Note records attached to the Case to store the generated PDF file. No customization is required on this entity.

## Placeholder Syntax

HTML templates use `{{fieldname}}` tokens where `fieldname` is the logical name of a Case attribute. At runtime the plugin replaces each token with the corresponding value from the Case record.

**Example template snippet:**

```html
<table>
  <tr><td>Client Name</td><td>{{new_clientname}}</td></tr>
  <tr><td>Permit Number</td><td>{{new_permitnumber}}</td></tr>
  <tr><td>Issue Date</td><td>{{createdon}}</td></tr>
</table>
```

**Supported field types:**

| CRM Type | Rendered As |
|---|---|
| String, Integer, Boolean | Direct `.ToString()` |
| DateTime | `dd-MMM-yyyy` (e.g. `08-Mar-2026`) |
| Money | Two-decimal number (e.g. `1,250.00`) |
| OptionSetValue | Numeric value |
| EntityReference (Lookup) | Record display name, falls back to GUID |

## Prerequisites

- Dynamics 365 CRM v9.x (on-premises or online)
- .NET Framework 4.6.2 or later
- NuGet dependencies:
  - `Microsoft.CrmSdk.CoreAssemblies` (v9.x)
  - `Newtonsoft.Json`
- The DocGen PDF API must be deployed and reachable from the CRM server

## Configuration

Update the following constants at the top of `PdfGenerationPlugin.cs` before building:

```csharp
private const string ApiBaseUrl = "https://your-api-url";
private const int StatusCompleted = 100000001;
private const int StatusResubmit = 100000002;
```

| Constant | Description |
|---|---|
| `ApiBaseUrl` | Base URL of the DocGen PDF API (no trailing slash) |
| `StatusCompleted` | Case `statuscode` option-set value for Completed |
| `StatusResubmit` | Case `statuscode` option-set value for Resubmit |

## Build

1. Open the plugin project in Visual Studio.
2. Restore NuGet packages.
3. Build in **Release** mode. The output assembly is `PdfGenerationPlugin.dll`.

## Plugin Registration (Plugin Registration Tool)

### Step 1 -- Register the Assembly

1. Open the **Plugin Registration Tool** (included in the CRM SDK / Power Platform CLI).
2. Connect to your Dynamics 365 organization.
3. Click **Register** > **Register New Assembly**.
4. Browse to `PdfGenerationPlugin.dll`.
5. Set isolation mode to **Sandbox** (online) or **None** (on-premises).
6. Set storage to **Database**.
7. Click **Register Selected Plugins**.

### Step 2 -- Register a Step

1. Select the registered assembly and expand it to find `PdfGenerationPlugin.GeneratePdfPlugin`.
2. Right-click > **Register New Step**.
3. Configure the step:

| Setting | Value |
|---|---|
| Message | `Update` |
| Primary Entity | `incident` |
| Filtering Attributes | `new_reporttype` |
| Event Pipeline Stage | **Post-Operation** |
| Execution Mode | **Asynchronous** (recommended) |
| Deployment | Server |

4. Click **Register New Step**.

### Step 3 -- Verify

1. Open a Case record in CRM.
2. Set the `new_reporttype` field to a value that matches a `new_pdftemplate` record (e.g. `permit_noc`).
3. Save the record.
4. Check the Notes section on the Case -- a PDF attachment should appear.
5. Verify the Case status reason has been updated.

## Troubleshooting

- **"No report template configured for type '...'"** -- No `new_pdftemplate` record exists with a `new_templatekey` matching the Case `new_reporttype` value. Create the template record.
- **"PDF API returned empty content"** -- The API responded but the `base64` field was empty. Check the API server logs.
- **"PDF generation failed"** -- Check the CRM plugin trace log (Settings > Plugin Trace Log) for the full exception details.
- **Network / timeout errors** -- Ensure the CRM server (or sandbox) can reach the `ApiBaseUrl` over HTTPS. The default timeout is 120 seconds.
- **Status update fails** -- Confirm the custom `statuscode` values exist on the Case entity and the constants in the plugin match.

## AGPL Compliance Note

This plugin does **not** include or use iText / iTextSharp. All PDF rendering is handled exclusively by the DocGen PDF API. The plugin only sends HTML and receives a base64-encoded PDF via HTTP.
