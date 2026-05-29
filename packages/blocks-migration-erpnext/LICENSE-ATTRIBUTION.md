# License Attribution — ERPNext / Frappe (ADR 0100 C4 (b) / spec §3.4 / §9.5)

## What this package does

`blocks-migration-erpnext` implements the A0 extraction adapter for the Harborline Software
ERPNext-to-Sunfish data importer. It reads ERPNext data from a mysqldump via a restored
throwaway database instance and maps the data into the frozen `Erpnext*Source` DTOs defined
in the Sunfish domain cluster packages.

## ERPNext and Frappe Framework

This package references the **data format** of ERPNext / Frappe — the `tab<DocType>` table
naming convention, standard DocType field labels, and child-table correlation conventions —
as a **data-interchange contract only**. No Frappe framework code (controllers, validators,
workflow logic, DocType-definition JSON, or server-side Python) is derived or included.

**ERPNext** is released under the [GNU General Public License v3.0 (GPLv3)](https://www.gnu.org/licenses/gpl-3.0.html).  
**Frappe Framework** is released under the [MIT License](https://opensource.org/licenses/MIT).

The `tab*` schema shape and column names used in this package are public data-format
references (Frappe public documentation + the observed dump structure) per ADR 0100 C4 and
importer spec §9.1–§9.5:

> C4 (b): The extraction adapter source file carries an attribution header naming
> ERPNext + Frappe + GPLv3 + "FORMAT-REFERENCE-ONLY; no Frappe code derived
> (data-interchange contract only per C4 / spec §9.5)."

This project (Harborline Software Shipyard) is released under the **MIT License**.

## No Frappe code included

This package does NOT include, copy, or derive from any Frappe or ERPNext source code.
The `tab*` table names and column names are the public schema of an ERPNext database
instance — they are the data format, not code — and are referenced solely for the purpose
of reading data out of a customer's own database for migration into Sunfish.
