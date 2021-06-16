﻿using HarmonyLib;
using Microsoft.Win32.SafeHandles;
using Ptc.Controls;
using Ptc.Controls.Text;
using Ptc.Controls.Core;
using Ptc.Controls.Worksheet;
using Ptc.Wpf;
using Ptc.PersistentData;
using Spirit;
using System;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Windows;
using Ptc.Controls.Whiteboard;
using System.Windows.Input;
using Ptc.FunctionalitiesLimitation;
using Networking;
using System.IO.Packaging;
using System.IO;
using Ptc.PersistentDataObjects;
using Ptc;
using Ptc.Serialization;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace ShareCad
{
    [HarmonyPatch]
    public class ShareCad
    {
        static EngineeringDocument engineeringDocument;
        static bool initializedModule = false;

        /// <summary>
        /// Initialize.
        /// </summary>
        public void ShareCadInit()
        {
            if (initializedModule)
            {
                return;
            }

            var harmony = new Harmony("ShareCad");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            WinConsole.Initialize();
            var sharecadControl = new ControllerWindow();
            sharecadControl.OnActivateShareFunctionality += SharecadControl_OnActivateShareFunctionality;
            sharecadControl.OnSyncPull += SharecadControl_OnSyncPull;
            sharecadControl.OnSyncPush += SharecadControl_OnSyncPush;
            sharecadControl.Show();

            Console.WriteLine("LOADED!");
            initializedModule = true;
        }

        private void SharecadControl_OnSyncPush()
        {
            //WorksheetControl control = (WorksheetControl)((EngineeringDocument)sender).Content;
            //var worksheetData = control.GetWorksheetData();

            var worksheetData = engineeringDocument.Worksheet.GetWorksheetData();

            if (worksheetData is null)
            {
                return;
            }

            using (Stream xmlStream = SerializeRegions(worksheetData.WorksheetContent))
            {
                Networking.Networking.TransmitStream(xmlStream);
            }
        }

        private void SharecadControl_OnSyncPull()
        {
            //WorksheetControl control = (WorksheetControl)((EngineeringDocument)sender).Content;
            //var worksheetData = control.GetWorksheetData();

            //var worksheetData = engineeringDocument.Worksheet.GetWorksheetData();

            //if (worksheetData is null)
            //{
                //Console.WriteLine("No progress :/");
            //}

            if (Networking.Networking.ReceiveXml(out string readXml))
            {
                Console.WriteLine("Incoming data:");
                DeserializeAndApplySection(readXml);
            }
            else
            {
                Console.WriteLine("No incoming data.");
            }
        }

        private void SharecadControl_OnActivateShareFunctionality(ControllerWindow.NetworkRole networkRole)
        {
            switch (networkRole)
            {
                case ControllerWindow.NetworkRole.Guest:
                    Networking.Networking.Client.Connect(IPAddress.Loopback);
                    break;
                case ControllerWindow.NetworkRole.Host:
                    Networking.Networking.Server.BindListener(IPAddress.Any);
                    break;
            }
        }

        // Fra MathcadPrime.exe
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpiritMainWindow), "NewDocument", new Type[] { typeof(bool), typeof(DocumentReadonlyOptions), typeof(bool) })]
        public static void Postfix_SpiritMainWindow(ref IEngineeringDocument __result)
        {
            engineeringDocument = __result as EngineeringDocument;
            Console.WriteLine("Retrieved document instance");
        }

        static bool subscribed = false;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WpfUtils), "ExecuteOnLayoutUpdated")]
        public static void Postfix_WpfUtils(ref UIElement element, ref Action action)
        {
            if (engineeringDocument is null)
            {
                return;
            }

            if (!subscribed)
            {
                engineeringDocument.Worksheet.PropertyChanged += Worksheet_PropertyChanged;
                subscribed = true;
            }
        }

        private static bool initializedTests;
        private static Package tempPackage;
        
        private static void Worksheet_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // register key inputs and other.
            if (!initializedTests)
            {
                initializedTests = true;

                // debug other stuff.
                //Networking.Networking.SendObject();

                string fileName = Path.GetTempPath() + Guid.NewGuid().ToString();

                tempPackage = Package.Open(fileName, FileMode.CreateNew, FileAccess.ReadWrite);

                // register keys
                CommandManager.RegisterClassCommandBinding(
                    typeof(WorksheetControl),
                    new CommandBinding(
                        WorksheetCommands.NewSolveBlock,
                        (o, localE) => SyncroniseExecuted(o, localE),
                        (_, localE) => { localE.CanExecute = true; }
                    ));

                /*
                CommandManager.RegisterClassInputBinding(
                    typeof(WorksheetControl), 
                    new InputBinding(new InputBindingFunctionalityCommandWrapper(WorksheetCommands.ToggleShowGrid), Gestures.CtrlUp));*/
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"=== {DateTime.Now:HH:mm:ss} - PropertyChange invoked - {e.PropertyName} ===");
            Console.ForegroundColor = ConsoleColor.Gray;

            WorksheetControl control = (WorksheetControl)sender;
            var worksheetData = control.GetWorksheetData();

            var viewModel = control.GetViewModel();

            //Console.WriteLine($" - ActiveItem: {control.ActiveItem}, {control.ActiveDescendant}, {control.CurrentElement}");
            // for at finde ud af hvad der gør dem unik så man kan sende et ID med over nettet.
            //Console.WriteLine($"ID: {control.PersistId}");

            // Liste over aktive elementer.
            //Console.WriteLine(" - Active section items:");
            //foreach (var item in worksheetData.WorksheetContent.RegionsToSerialize)
            //{
            //    Console.WriteLine($"{item.Key}");
            //}

            /*
            Console.WriteLine();

            switch (e.PropertyName)
            {
                case "SelectedDescendants":
                    #region Testing
                    // finder det første element lavet, eller null.
                    //var firstElement = control.ActiveSectionItems.FirstOrDefault();

                    // aktivér debug test scenarie hvis der laves en tekstboks som det første element.
                    //if (firstElement is TextRegion realText)
                    //{
                        //Console.WriteLine("First element is text");

                        // flyt det første element til koordinatet (0, 5)
                        //control.MoveItemGridLocation(firstElement, new Point(0, 2));

                        //realText.Text = "👑〖⚡ᖘ๖ۣۜℜΘ𝕵ECT ΘVERRIDE⚡〗👑";

                        // Prøv at oprette et tekst element, (der bliver ikke gjort mere ved det lige nu).
                        //Ptc.Controls.Text.TextRegion textRegion = new Ptc.Controls.Text.TextRegion()
                        //{
                        //    Text = "INJECTED!",
                        //};

                        // Indsæt tekst element.
                        //viewModel.AddItemAtLocation(textRegion, viewModel.GridLocationToWorksheetLocation(new Point(5, 7)));

                        // Profit! (andre test ting)
                        //if (worksheetData is null)
                        //{
                        //    break;
                        //}
                        //
                        //using (Stream xmlStream = SerializeRegions(worksheetData.WorksheetContent))
                        //{
                        //    Networking.Networking.TransmitStream(xmlStream);
                        //}

                        //TcpClient client = new TcpClient("192.168.2.215", 8080);
                        //var tcpStream = client.GetStream();
                        //Networking.Networking.Server.BindListener(IPAddress.Loopback);
                    //}
                    //else if (firstElement is SolveBlockControl solveBlock)
                    //{
                    //    Console.WriteLine("First element is solveblock");
                    //    Networking.Networking.Client.Connect(IPAddress.Loopback);
                    //}
                    #endregion
                    break;
                case "CurrentElement":
                    break;
                case "WorksheetPageLayoutMode":
                    // changed from draft to page
                    break;
                default:
                    break;
            }*/
        }

        /// <summary>
        /// Et forsøg på at have en event ting.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="e"></param>
        private static void SyncroniseExecuted(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private static List<IRegionPersistentData> DeserializeSection(Stream serializedSection, IWorksheetSectionPersistentData sectionData)
        {
            #region old
            //worksheetRegionCollectionSerializer regionCollectionSerializer = new worksheetRegionCollectionSerializer();

            // deserialize regions
            // DeserializeWorksheetSection(this.PackageOpsProvider.GetMainPartFromPackage(package), this.WorksheetData.WorksheetContent, worksheetRegionCollectionSerializer, fullFilePath);
            // DeserializeWorksheetSection(PackagePart, IWorksheetSectionPersistentData, IRegionCollectionSerializer, string);

            /*
            MethodInfo dynMethod = engineeringDocument.GetType().GetMethod("DeserializeWorksheetSection", BindingFlags.NonPublic | BindingFlags.Instance);
            dynMethod.Invoke(engineeringDocument, new object[]
            {
                deserializeableSection,
                worksheetContent,
                worksheetRegionCollectionSerializer,
                ""
            });*/
            #endregion

            worksheetRegionCollectionSerializer regionCollectionSerializer = new worksheetRegionCollectionSerializer();

            using (CustomMcdxDeserializer mcdxDeserializer =
                new CustomMcdxDeserializer(
                    null,
                    new CustomWorksheetSectionDeserializationStrategy(
                        sectionData,
                        engineeringDocument.MathFormat,
                        engineeringDocument.LabeledIdFormat
                        ),
                    engineeringDocument.DocumentSerializationHelper,
                    regionCollectionSerializer,
                    true
                    )
                )
            {
                mcdxDeserializer.Deserialize(serializedSection);
                return (List<IRegionPersistentData>)mcdxDeserializer.DeserializedRegions;
            }
        }

        private static void DeserializeAndApplySection(string xml)
        {
            worksheetRegionCollectionSerializer regionCollectionSerializer = new worksheetRegionCollectionSerializer();

            IWorksheetPersistentData worksheetData = new WorksheetPersistentData() 
            {
                DisplayGrid = engineeringDocument.WorksheetData.DisplayGrid,
                GridSize = engineeringDocument.WorksheetData.GridSize,
                LayoutSize = engineeringDocument.WorksheetData.LayoutSize,
                MarginType = engineeringDocument.WorksheetData.MarginType,
                DisplayHFGrid = engineeringDocument.WorksheetData.DisplayHFGrid,
                OleObjectAutoResize = engineeringDocument.WorksheetData.OleObjectAutoResize,
                PageOrientation = engineeringDocument.WorksheetData.PageOrientation,
                PlotBackgroundType = engineeringDocument.WorksheetData.PlotBackgroundType,
                ShowIOTags = engineeringDocument.WorksheetData.ShowIOTags
            };

            using (CustomMcdxDeserializer mcdxDeserializer =
                new CustomMcdxDeserializer(
                    null,
                    new CustomWorksheetSectionDeserializationStrategy(
                        worksheetData.WorksheetContent,
                        engineeringDocument.MathFormat,
                        engineeringDocument.LabeledIdFormat
                        ),
                    engineeringDocument.DocumentSerializationHelper,
                    regionCollectionSerializer,
                    true
                    )
                )
            {
                mcdxDeserializer.Deserialize(xml);
                engineeringDocument.DocumentSerializationHelper.MainRegions = mcdxDeserializer.DeserializedRegions;

                engineeringDocument.Worksheet.ApplyWorksheetData(worksheetData);
            }
        }

        private static Stream SerializeRegions(IWorksheetSectionPersistentData serializableSection)
        {
            // Delete part if it already exists in tempPackage.
            var regionFileLocation = new Uri("/regions.xml", UriKind.Relative);
            tempPackage.DeletePart(regionFileLocation);

            var part = tempPackage.CreatePart(regionFileLocation, System.Net.Mime.MediaTypeNames.Text.Xml);

            MethodInfo dynMethod = engineeringDocument.GetType().GetMethod("SerializeWorksheetSection", BindingFlags.NonPublic | BindingFlags.Instance);
            dynMethod.Invoke(engineeringDocument, new object[]
            {
                part,
                serializableSection,
                (DelegateFunction0<IRegionType>)(() => new worksheetRegionType()),
                new worksheetRegionCollectionSerializer()
            });

            return part.GetStream();
        }
    }
}
