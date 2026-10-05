using System;
using System.Collections.Generic;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Builds the demo dataset. The checklist content is transcribed from the Mzukulu
    /// MZT-QC-3xx checklists and the MZT-QC-203 register supplied with the brief.
    /// </summary>
    internal static class Seed
    {
        private static int _idCounter;

        private static string Id(string prefix)
        {
            _idCounter++;
            return prefix + "_" + _idCounter.ToString("D4");
        }

        /// <summary>Creates a section from an array of {text, refLabel} pairs.</summary>
        private static TemplateSection Section(string title, params string[][] rows)
        {
            TemplateSection section = new TemplateSection { Id = Id("sec"), Title = title };

            foreach (string[] row in rows)
            {
                section.Items.Add(new TemplateItem
                {
                    Id = Id("itm"),
                    Text = row[0],
                    RefLabel = row.Length > 1 ? row[1] : string.Empty,
                    RefRequired = row.Length > 1 && !string.IsNullOrEmpty(row[1])
                });
            }

            return section;
        }

        private static string[] R(string text)
        {
            return new[] { text };
        }

        private static string[] R(string text, string refLabel)
        {
            return new[] { text, refLabel };
        }

        public static Database Build()
        {
            _idCounter = 0;

            Database db = new Database();

            const string clientId = "cli-hulett";
            const string projectId = "prj-hulett-factory";

            db.Clients.Add(new Client
            {
                Id = clientId,
                Name = "Hulett",
                Contact = "Sipho Ndlovu · sipho.ndlovu@hulett.co.za · 031 555 0142",
                Active = true
            });

            db.Projects.Add(new Project
            {
                Id = projectId,
                ClientId = clientId,
                Name = "Hulett Factory Electrical Installation",
                Number = "MZT-2026-014",
                ContractNo = "HUL/EL/2026/03",
                Site = "Hulett Sugar Refinery, Durban",
                Description = "New electrical reticulation for the factory expansion — underground and "
                              + "above-ground cabling, racking and field enclosures.",
                Status = "Active",
                StartDate = new DateTime(2026, 2, 10),
                EndDate = new DateTime(2026, 11, 30)
            });

            db.Templates.AddRange(BuildTemplates());
            AddDemoRecords(db, projectId);
            AddDemoUsers(db);

            return db;
        }

        /// <summary>
        /// Seeds the two demo accounts as real, password-protected users so the system
        /// is usable immediately. Credentials are shown on the login screen.
        /// </summary>
        private static void AddDemoUsers(Database db)
        {
            EnsureDefaultUsers(db);
        }

        /// <summary>
        /// Adds the default admin and site-user accounts if they are missing. Safe to
        /// call on an existing database; it only fills gaps.
        /// </summary>
        public static void EnsureDefaultUsers(Database db)
        {
            if (db.Users == null) db.Users = new List<UserAccount>();

            if (!db.Users.Exists(u => string.Equals(u.Username, "admin", StringComparison.OrdinalIgnoreCase)))
            {
                db.Users.Add(Auth.CreateSeedAccount(
                    "usr-admin", "admin", "S. Ndlovu", "s.ndlovu@mzukulu.co.za",
                    "admin123", UserRole.Admin));
            }

            if (!db.Users.Exists(u => string.Equals(u.Username, "siteuser", StringComparison.OrdinalIgnoreCase)))
            {
                db.Users.Add(Auth.CreateSeedAccount(
                    "usr-user", "siteuser", "T. Mahlangu", "t.mahlangu@mzukulu.co.za",
                    "user123", UserRole.SiteUser));
            }
        }

        private static List<ChecklistTemplate> BuildTemplates()
        {
            var list = new List<ChecklistTemplate>();

            // ---- MZT-QC-301 -------------------------------------------------
            list.Add(new ChecklistTemplate
            {
                Id = "tpl-301",
                DocNumber = "MZT-QC-301",
                DocName = "Cable Trenching & Installation",
                Description = "Underground cable route — excavation, bedding, laying and reinstatement",
                Revision = "Rev 1",
                IssueDate = new DateTime(2025, 1, 15),
                Active = true,
                ProjectFields = new List<string> { "Trench section / Ref.", "Cable N° / Schedule ref." },
                Scope = "Work to conform to the latest 'Approved for Construction' drawings, the approved "
                        + "cable schedule, the project standard trench detail and SANS 10142-1.",
                Acceptance = "Trench may not be covered until every line below is signed 'A' or a closed-out "
                             + "NCR is referenced.",
                Sections = new List<TemplateSection>
                {
                    Section(string.Empty,
                        R("Route of the excavation is as per the AFC drawing; existing services proved and permit to excavate in place."),
                        R("Excavated trench width and depth are as per the standard drawing.", "Depth / Width (mm)"),
                        R("Trench floor is free of debris, stones, roots and standing water."),
                        R("Sleeve / kick-pipe size, quantity and location are as per drawing."),
                        R("50 mm bedding of clean sieved sand placed and levelled before cables are laid."),
                        R("Cables installed as per AFC drawings and the approved cable schedule."),
                        R("Cables correctly numbered and tagged at both ends and at 5 m intervals."),
                        R("Cables properly dressed, laid without crossovers and with correct phase / group spacing."),
                        R("Cables free of kinks and surface damage; minimum bending radius maintained."),
                        R("Cable continuity and insulation tests completed and results recorded.", "Certificate N°"),
                        R("Trench back-filled and compacted with clean sieved sand to 300 mm below finished grade."),
                        R("Warning / protection tiles fitted continuously over the full cable run."),
                        R("Cable warning tape installed at 300 mm below finished grade."),
                        R("Trench back-filled and compacted with surrounding earth to finished grade; surface reinstated."),
                        R("Cables emerging from underground protected by kick pipes and correctly sealed."),
                        R("Route markers installed at changes of direction, at joints and at road crossings."),
                        R("All field revisions recorded on the 'As-Built' drawing set."))
                }
            });

            // ---- MZT-QC-302 -------------------------------------------------
            list.Add(new ChecklistTemplate
            {
                Id = "tpl-302",
                DocNumber = "MZT-QC-302",
                DocName = "Underground Cable Installation",
                Description = "Cable pulling, laying and protection in trench, sleeve and duct",
                Revision = "Rev 1",
                IssueDate = new DateTime(2025, 1, 15),
                Active = true,
                ProjectFields = new List<string> { "Trench / Duct section", "Cable N° / Type" },
                Scope = "Pulling tension and bending radius shall not exceed the cable manufacturer's "
                        + "published limits.",
                Acceptance = "Cables shall be tested before and after pulling — record both certificate numbers.",
                Sections = new List<TemplateSection>
                {
                    Section(string.Empty,
                        R("Route of the excavation is as per the AFC drawing."),
                        R("Excavated trench width and depth are as per the standard drawing."),
                        R("Trench floor free of debris, stones, roots and standing water."),
                        R("Sleeve size, position and bell-ends are as per drawing."),
                        R("50 mm bedding of clean sieved sand placed before cables are laid."),
                        R("Drum test certificate available and acceptable prior to pulling.", "Certificate N°"),
                        R("Cable rollers, bends and pulling equipment correctly set up; pulling eye / stocking correct."),
                        R("Pulling tension monitored and within the manufacturer's limit.", "Max tension (kN)"),
                        R("Cables installed as per AFC drawings and the approved cable schedule."),
                        R("Cables correctly numbered and tagged at both ends and at 5 m intervals."),
                        R("Cables properly dressed with adequate slack left at both terminations."),
                        R("Cables free of kinks and surface damage; minimum bending radius maintained."),
                        R("Post-installation continuity and insulation tests completed and recorded.", "Certificate N°"),
                        R("Trench back-filled and compacted with clean sieved sand to 350 mm below grade."),
                        R("Protection tiles and warning tape fitted."),
                        R("Trench back-filled and compacted with surrounding earth to finished grade."),
                        R("Cables emerging from underground protected by kick pipes; kick pipes correctly sealed."),
                        R("All field revisions recorded on the 'As-Built' drawing set."))
                }
            });

            // ---- MZT-QC-303 -------------------------------------------------
            list.Add(new ChecklistTemplate
            {
                Id = "tpl-303",
                DocNumber = "MZT-QC-303",
                DocName = "Underground Sleeve & Conduit Installation",
                Description = "Sleeves, ducts and road crossings prior to cable pulling",
                Revision = "Rev 1",
                IssueDate = new DateTime(2025, 1, 15),
                Active = true,
                ProjectFields = new List<string> { "Location / Chainage", "Sleeve size & type" },
                Scope = "Conduit and sleeve installation to conform to SANS 10142-1 and the project "
                        + "standard drawings.",
                Acceptance = "Total bend in any one system shall not exceed 360° without an intermediate pull box.",
                Sections = new List<TemplateSection>
                {
                    Section(string.Empty,
                        R("Trenching checked for location, elevation and levelness and is free of debris."),
                        R("Pipe size, location and routing conform to drawings; material correct and undamaged."),
                        R("Field bend radius correct per drawing and pipe free of deformities or flattening."),
                        R("Trenching checked for interference with underground piping, earthing and other services."),
                        R("Seals installed on all exposed pipe ends before concrete is poured."),
                        R("Underground sleeves extend at least 750 mm past the edge of the road crossing."),
                        R("Bell-ends fitted to all conduit terminations."),
                        R("Long-radius bends correctly jointed to conduit; joints solvent-welded / coupled as specified."),
                        R("Stub-ups maintain 50 mm spacing from the structure and are correctly supported."),
                        R("Conduit left with end caps and pulling ropes installed."),
                        R("Conduit cleaned with mandrel and swabbing brush prior to pulling cable."),
                        R("Earthing / bonding completed where RGS (galvanised steel) conduit is used."),
                        R("Duct seals installed where conduit enters or leaves a hazardous area classification."),
                        R("Conduit closed up with duct seal after cable installation."),
                        R("Back-fill and compaction test performed as required.", "Result (%)"),
                        R("Conduit and installation conform to SANS standards and project specification."),
                        R("All field revisions recorded on the 'As-Built' drawing set."))
                }
            });

            // ---- MZT-QC-304 -------------------------------------------------
            list.Add(new ChecklistTemplate
            {
                Id = "tpl-304",
                DocNumber = "MZT-QC-304",
                DocName = "Manufacture & Installation of Cable Racks",
                Description = "Cable ladder, tray and support steelwork",
                Revision = "Rev 1",
                IssueDate = new DateTime(2025, 1, 15),
                Active = true,
                ProjectFields = new List<string> { "Cable rack section", "Rack type & size" },
                Scope = "Racking, brackets and fixings to be as per AFC drawings and the manufacturer's "
                        + "installation instructions.",
                Acceptance = "Earth continuity across the full racking run must be proved and recorded before "
                             + "cables are installed.",
                Sections = new List<TemplateSection>
                {
                    Section(string.Empty,
                        R("Cable racks and support brackets manufactured to AFC drawings."),
                        R("Material, finish and corrosion protection as per specification."),
                        R("Cable rack route as per AFC drawing."),
                        R("Support brackets evenly spaced and correctly mounted; span within manufacturer's limits."),
                        R("Installed racking is the correct size, orientation and elevation."),
                        R("Minimum separation from hot surfaces, process piping and instrument routes maintained."),
                        R("Installed racking does not obstruct walkways, access routes or equipment removal paths."),
                        R("Racking securely fixed to supports with the correct specified bolts, nuts and washers."),
                        R("Installed sections correctly joined with splice plates and the specified fasteners."),
                        R("All bolts torqued as per manufacturer specification.", "Torque (Nm)"),
                        R("Cut and welded sections cleaned and coated by the client-approved method."),
                        R("Segregation between power, control and instrument racks maintained as specified."),
                        R("Racking bonded, electrically continuous and connected to the main earth."),
                        R("Earth continuity test completed and results recorded.", "Certificate N°"),
                        R("Covers / lids fitted where specified and correctly secured."),
                        R("All field revisions recorded on the 'As-Built' drawing set."))
                }
            });

            // ---- MZT-QC-305 -------------------------------------------------
            list.Add(new ChecklistTemplate
            {
                Id = "tpl-305",
                DocNumber = "MZT-QC-305",
                DocName = "Above-Ground Cable Installation",
                Description = "Cables installed on racking, tray, trunking and cleats",
                Revision = "Rev 1",
                IssueDate = new DateTime(2025, 1, 15),
                Active = true,
                ProjectFields = new List<string> { "Cable rack section", "Cable N° / Schedule ref." },
                Scope = "Racking must be signed off on MZT-QC-304 before cables are installed on it.",
                Acceptance = "Cable spacing, segregation and fixing centres to be as per specification.",
                Sections = new List<TemplateSection>
                {
                    Section(string.Empty,
                        R("Cable size, type, location and routing are as per the AFC cable schedule."),
                        R("Cable racks approved for cable installation (MZT-QC-304 signed off)."),
                        R("Cable reference tags fitted at both ends and at required intervals, as per cable schedule."),
                        R("Cables free of kinks, flattening and surface damage."),
                        R("Cable bending radius correct at all changes of direction."),
                        R("Cables installed without crossovers and strapped to tray at the specified centres."),
                        R("Correct segregation maintained between power, control, instrument and IS circuits."),
                        R("Cable cleats / straps of correct type, UV-rated where exposed, and correctly tensioned."),
                        R("Cables exit tray from the rear / underside as per standard detail."),
                        R("Adequate slack left at both terminations for glanding and future re-termination."),
                        R("Vertical runs adequately supported; weight not carried on glands or terminations."),
                        R("Fire-stopping / barriers reinstated at all wall and floor penetrations."),
                        R("Continuity and insulation tests performed and results recorded.", "Certificate N°"),
                        R("All revisions recorded on the 'As-Built' drawing set."))
                }
            });

            // ---- MZT-QC-306 (three sections) --------------------------------
            list.Add(new ChecklistTemplate
            {
                Id = "tpl-306",
                DocNumber = "MZT-QC-306",
                DocName = "Electrical Enclosure Installation",
                Description = "Junction boxes, marshalling cabinets, local panels and field enclosures",
                Revision = "Rev 1",
                IssueDate = new DateTime(2025, 1, 15),
                Active = true,
                ProjectFields = new List<string> { "Enclosure / Cabinet N°", "Area classification & IP rating" },
                Scope = "To prevent ingress of dust and water, doors and covers are to be opened only while "
                        + "work is in progress.",
                Acceptance = "Where the enclosure is installed in a classified area, the Ex certification must "
                             + "be verified and recorded.",
                Sections = new List<TemplateSection>
                {
                    Section("Enclosure & Mounting",
                        R("Installation conforms to the area classification and the specified IP rating."),
                        R("Identification and warning labels fitted and legible."),
                        R("Location, elevation and orientation as per drawing."),
                        R("Enclosure properly mounted with no uncertified modifications or unauthorised drilling."),
                        R("Correct separation maintained between the enclosure and adjacent equipment / hot surfaces."),
                        R("Suitably positioned for safe access by operations and maintenance."),
                        R("All brackets secured, bolts cut to correct length, cut edges corrosion-protected.")),

                    Section("Cable Entry & Termination",
                        R("Correct cable glands installed, tight, with IP washers fitted; unused entries blanked with certified plugs."),
                        R("Cables correctly secured with no strain on cable glands."),
                        R("Cables correctly tagged and circuit numbers installed as per schedule."),
                        R("Terminal rails secured and spacing correct; terminals of correct type and rating."),
                        R("Terminals secured with correct end stops and end-shield assemblies fitted."),
                        R("Phase separation integrity maintained (barriers / live-part shrouding installed)."),
                        R("Terminal identification markers installed and matching the termination schedule."),
                        R("Core / wire markers installed at both ends."),
                        R("Correct lugs installed, fully crimped, with no excess exposed copper."),
                        R("All terminals checked for tightness.", "Torque (Nm)")),

                    Section("Internals, Earthing & Close-Out",
                        R("Internals clean, dry and free of moisture and construction debris."),
                        R("All circuit breakers, fuses and links verified for continuity, rating and type."),
                        R("Internal wiring layout neat, tidy and correctly routed in trunking."),
                        R("Internal and external earthing and bonding complete; earth sleeves / tape installed."),
                        R("Neutrals identified and terminated in the correct sequence."),
                        R("Gaskets and seals undamaged and correctly fitted; weather-proofing devices installed."),
                        R("No visible damage; all doors and covers installed and secured."),
                        R("All revisions recorded on the 'As-Built' drawing set."))
                }
            });

            return list;
        }

        /// <summary>Creates the three demo forms and the demo register.</summary>
        private static void AddDemoRecords(Database db, string projectId)
        {
            // --- QC-0001: MZT-QC-301, in progress with one rejected line -----
            ChecklistTemplate t301 = db.Templates.First(t => t.Id == "tpl-301");
            var items301 = t301.AllItems();
            QcForm f1 = new QcForm
            {
                Id = "frm-demo-301a",
                TemplateId = t301.Id,
                ProjectId = projectId,
                QcRecordNo = "QC-0001",
                Status = RecordStatus.InProgress,
                Remarks = "Protection tiles for the final 40 m section not yet installed — awaiting tile delivery.",
                NcrNo = "NCR-0007",
                CreatedAt = new DateTime(2026, 3, 12, 8, 20, 0),
                UpdatedAt = new DateTime(2026, 3, 14, 15, 5, 0),
                CreatedBy = "Site User"
            };
            f1.FieldValues["Trench section / Ref."] = "TR-01 (Sub A to MDB1)";
            f1.FieldValues["Cable N° / Schedule ref."] = "C-014 / Sched Rev C";

            for (int i = 0; i < items301.Count; i++)
            {
                ItemResponse resp = i < 11 ? ItemResponse.Accepted
                                   : (i == 11 ? ItemResponse.Rejected : ItemResponse.Unanswered);
                f1.Items.Add(new FormItemResult
                {
                    ItemId = items301[i].Id,
                    Response = resp,
                    Ref = i == 1 ? "Depth 900 mm  Width 450 mm" : string.Empty,
                    Initials = i < 12 ? "TM" : string.Empty,
                    Date = i < 12 ? new DateTime(2026, 3, 14) : (DateTime?)null
                });
            }
            f1.Inspected = new SignoffEntry { Name = "T. Mahlangu", Date = new DateTime(2026, 3, 14) };
            db.Forms.Add(f1);

            // --- QC-0002: MZT-QC-305, complete and accepted ------------------
            ChecklistTemplate t305 = db.Templates.First(t => t.Id == "tpl-305");
            QcForm f2 = new QcForm
            {
                Id = "frm-demo-305a",
                TemplateId = t305.Id,
                ProjectId = projectId,
                QcRecordNo = "QC-0002",
                Status = RecordStatus.CompleteAccepted,
                CreatedAt = new DateTime(2026, 4, 1, 9, 0, 0),
                UpdatedAt = new DateTime(2026, 4, 4, 11, 0, 0),
                CreatedBy = "Site User"
            };
            f2.FieldValues["Cable rack section"] = "Rack Run 3 — Substation to MCC2";
            f2.FieldValues["Cable N° / Schedule ref."] = "C-021 to C-034";

            foreach (TemplateItem item in t305.AllItems())
            {
                f2.Items.Add(new FormItemResult
                {
                    ItemId = item.Id,
                    Response = ItemResponse.Accepted,
                    Ref = item.HasRef ? "Cert. N° EI-2231" : string.Empty,
                    Initials = "TM",
                    Date = new DateTime(2026, 4, 2)
                });
            }
            f2.Inspected = new SignoffEntry { Name = "T. Mahlangu", Signature = "TM", Date = new DateTime(2026, 4, 2) };
            f2.Reviewed = new SignoffEntry { Name = "B. Reddy", Signature = "BR", Date = new DateTime(2026, 4, 3) };
            f2.Approved = new SignoffEntry { Name = "S. Ndlovu", Signature = "SN", Date = new DateTime(2026, 4, 4) };
            db.Forms.Add(f2);

            // --- QC-0003: MZT-QC-306, partially complete ---------------------
            ChecklistTemplate t306 = db.Templates.First(t => t.Id == "tpl-306");
            var items306 = t306.AllItems();
            QcForm f3 = new QcForm
            {
                Id = "frm-demo-306a",
                TemplateId = t306.Id,
                ProjectId = projectId,
                QcRecordNo = "QC-0003",
                Status = RecordStatus.InProgress,
                CreatedAt = new DateTime(2026, 5, 6, 7, 40, 0),
                UpdatedAt = new DateTime(2026, 5, 6, 12, 10, 0),
                CreatedBy = "Site User"
            };
            f3.FieldValues["Enclosure / Cabinet N°"] = "JB-014";
            f3.FieldValues["Area classification & IP rating"] = "Zone 2 / IP65";

            for (int i = 0; i < items306.Count; i++)
            {
                f3.Items.Add(new FormItemResult
                {
                    ItemId = items306[i].Id,
                    Response = i < 10 ? ItemResponse.Accepted : ItemResponse.Unanswered,
                    Ref = string.Empty,
                    Initials = i < 10 ? "PN" : string.Empty,
                    Date = i < 10 ? new DateTime(2026, 5, 6) : (DateTime?)null
                });
            }
            db.Forms.Add(f3);

            // --- QC-0004: MZT-QC-203 register --------------------------------
            CableRegister reg = new CableRegister
            {
                Id = "reg-demo-1",
                ProjectId = projectId,
                QcRecordNo = "QC-0004",
                Status = RecordStatus.InProgress,
                Notes = "Cables C-014/C-015 pulled together in TR-01 — see MZT-QC-301 QC-0001 for trench sign-off.",
                CreatedAt = new DateTime(2026, 3, 12, 8, 0, 0),
                UpdatedAt = new DateTime(2026, 4, 5, 10, 0, 0),
                CreatedBy = "Site User"
            };
            reg.FieldValues["Project"] = "Hulett Factory Electrical Installation";
            reg.FieldValues["Project N°"] = "MZT-2026-014";
            reg.FieldValues["Client"] = "Hulett";
            reg.FieldValues["Contract / Order N°"] = "HUL/EL/2026/03";
            reg.FieldValues["Area / Location"] = "Sub A — MDB1 Feeders";
            reg.FieldValues["Drawing N° & Rev."] = "E-1042 Rev C";
            reg.FieldValues["Cable Schedule Ref. & Rev."] = "CS-014 Rev C";
            reg.FieldValues["Register Revision"] = "Rev 2";

            reg.Cables.Add(new CableRow
            {
                Id = Id("cab"), No = 1, CableNo = "C-014", From = "Sub A", To = "MDB1",
                CableType = "3C x 95mm² Cu XLPE", DrumNo = "D-2201", Length = "145",
                Pulled = true, Glanded = true, Terminated = true,
                IrCert = "IR-3301", ContCert = "CT-3301", Complete = true
            });
            reg.Cables.Add(new CableRow
            {
                Id = Id("cab"), No = 2, CableNo = "C-015", From = "Sub A", To = "MDB1",
                CableType = "3C x 95mm² Cu XLPE", DrumNo = "D-2201", Length = "148",
                Pulled = true, Glanded = true, Terminated = false,
                IrCert = string.Empty, ContCert = string.Empty, Complete = false
            });
            reg.Cables.Add(new CableRow
            {
                Id = Id("cab"), No = 3, CableNo = "C-021", From = "MCC2", To = "Pump P-04",
                CableType = "4C x 16mm² Cu XLPE", DrumNo = "D-2214", Length = "62",
                Pulled = true, Glanded = false, Terminated = false,
                IrCert = string.Empty, ContCert = string.Empty, Complete = false
            });
            reg.Cables.Add(new CableRow
            {
                Id = Id("cab"), No = 4, CableNo = "C-022", From = "MCC2", To = "Pump P-05",
                CableType = "4C x 16mm² Cu XLPE", DrumNo = "D-2214", Length = "58",
                Pulled = false, Glanded = false, Terminated = false,
                IrCert = string.Empty, ContCert = string.Empty, Complete = false
            });

            reg.MaintainedBy = new SignoffEntry
            {
                Name = "Mzukulu Technologies — QC",
                Signature = "T. Mahlangu",
                Date = new DateTime(2026, 4, 5)
            };
            db.Registers.Add(reg);

            AddDemoNcrs(db, projectId);
            AddDemoAudit(db);
        }

        /// <summary>Two demo NCRs: one open and overdue, one closed out.</summary>
        private static void AddDemoNcrs(Database db, string projectId)
        {
            db.Ncrs.Add(new Ncr
            {
                Id = "ncr-0007",
                NcrNo = "NCR-0007",
                ProjectId = projectId,
                FormId = "frm-demo-301a",
                Title = "Protection tiles not installed over final 40 m of TR-01",
                Description = "Trench TR-01 back-filled without warning tiles over the last 40 m. "
                              + "Tiles were not on site at the time of back-fill.",
                Severity = NcrSeverity.Major,
                State = NcrState.InRemediation,
                RaisedBy = "T. Mahlangu",
                RaisedAt = new DateTime(2026, 3, 14, 15, 10, 0),
                AssignedTo = "B. Reddy",
                DueDate = new DateTime(2026, 3, 28),
                RootCause = "Tile delivery not sequenced against the trenching programme.",
                CorrectiveAction = "Expose final 40 m, install tiles and warning tape, re-inspect."
            });

            db.Ncrs.Add(new Ncr
            {
                Id = "ncr-0006",
                NcrNo = "NCR-0006",
                ProjectId = projectId,
                FormId = null,
                Title = "Incorrect gland type fitted to JB-009",
                Description = "Brass glands fitted where the Ex certification requires stainless.",
                Severity = NcrSeverity.Critical,
                State = NcrState.Closed,
                RaisedBy = "S. Ndlovu",
                RaisedAt = new DateTime(2026, 2, 18, 9, 0, 0),
                AssignedTo = "T. Mahlangu",
                DueDate = new DateTime(2026, 3, 4),
                RootCause = "Store issued the wrong gland kit; no check against the Ex schedule.",
                CorrectiveAction = "All glands on JB-009 replaced with certified stainless items and re-tested.",
                ClosedBy = "S. Ndlovu",
                ClosedAt = new DateTime(2026, 2, 27, 14, 30, 0)
            });
        }

        /// <summary>Seeds a plausible audit history so the log is not empty on first run.</summary>
        private static void AddDemoAudit(Database db)
        {
            Action<DateTime, string, UserRole, AuditAction, string, string, string, string> add =
                (at, user, role, action, type, id, label, detail) =>
            {
                db.Audit.Add(new AuditEntry
                {
                    Id = "aud_" + db.Audit.Count.ToString("D4"),
                    At = at,
                    User = user,
                    Role = role,
                    Action = action,
                    EntityType = type,
                    EntityId = id,
                    EntityLabel = label,
                    Detail = detail
                });
            };

            add(new DateTime(2026, 2, 10, 8, 5, 0), "S. Ndlovu", UserRole.Admin,
                AuditAction.Created, "Project", "prj-hulett-factory",
                "Hulett Factory Electrical Installation", "Project created");

            add(new DateTime(2026, 2, 18, 9, 0, 0), "S. Ndlovu", UserRole.Admin,
                AuditAction.NcrRaised, "NCR", "ncr-0006", "NCR-0006",
                "Critical · Incorrect gland type fitted to JB-009");

            add(new DateTime(2026, 2, 27, 14, 30, 0), "S. Ndlovu", UserRole.Admin,
                AuditAction.NcrClosed, "NCR", "ncr-0006", "NCR-0006",
                "Closed out by S. Ndlovu");

            add(new DateTime(2026, 3, 12, 8, 20, 0), "T. Mahlangu", UserRole.SiteUser,
                AuditAction.Created, "Form", "frm-demo-301a", "MZT-QC-301 · QC-0001",
                "Checklist started");

            add(new DateTime(2026, 3, 14, 15, 5, 0), "T. Mahlangu", UserRole.SiteUser,
                AuditAction.Updated, "Form", "frm-demo-301a", "MZT-QC-301 · QC-0001",
                "12 of 17 items answered");

            add(new DateTime(2026, 3, 14, 15, 10, 0), "T. Mahlangu", UserRole.SiteUser,
                AuditAction.NcrRaised, "NCR", "ncr-0007", "NCR-0007",
                "Major · Protection tiles not installed over final 40 m of TR-01");

            add(new DateTime(2026, 4, 2, 16, 0, 0), "T. Mahlangu", UserRole.SiteUser,
                AuditAction.SignedOff, "Form", "frm-demo-305a", "MZT-QC-305 · QC-0002",
                "Inspected / Tested By signed");

            add(new DateTime(2026, 4, 4, 11, 0, 0), "S. Ndlovu", UserRole.Admin,
                AuditAction.Submitted, "Form", "frm-demo-305a", "MZT-QC-305 · QC-0002",
                "Submitted — Complete — Accepted");

            add(new DateTime(2026, 5, 6, 12, 10, 0), "T. Mahlangu", UserRole.SiteUser,
                AuditAction.Updated, "Form", "frm-demo-306a", "MZT-QC-306 · QC-0003",
                "10 of 25 items answered");
        }
    }
}
