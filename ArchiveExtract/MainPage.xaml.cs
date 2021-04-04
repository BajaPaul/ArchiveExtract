using LibraryCoder.LibZipArchive;
using LibraryCoder.MainPageCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ArchiveExtract
{
    // Following Enum is generally unique for each App so place here.
    /// <summary>
    /// Enum used to reset App setup value via method AppReset().
    /// </summary>
    public enum EnumResetApp { DoNothing, ResetApp, ResetPurchaseHistory, ResetRateHistory, ShowDataStoreValues };

    public sealed partial class MainPage : Page
    {
        // TODO: Update version number in next string to match version number set in Package.appmanifest file before publishing App to Store.
        /// <summary>
        /// String containing version of application. Should match value set in Package.appxmanifest file.
        /// </summary>
        private readonly string stringAppVersion = "2021.4.4";

        /// <summary>
        /// Location application uses to read or write various application settings.
        /// </summary>
        private ApplicationDataContainer applicationDataContainer;

        // ALL data store 'ds' strings (keys) used by application declared here. These are (key, value) pairs. Each key has a matching value.

        /// <summary>
        /// Value is "BoolAppPurchased".
        /// </summary>
        private readonly string ds_BoolAppPurchased = "BoolAppPurchased";

        /// <summary>
        /// Value is "BoolAppRated".
        /// </summary>
        private readonly string ds_BoolAppRated = "BoolAppRated";

        /// <summary>
        /// Value is "IntAppRatedCounter".
        /// </summary>
        private readonly string ds_IntAppRatedCounter = "IntAppRatedCounter";

        /// <summary>
        /// Value is "CompressionLevelApp".
        /// </summary>
        private readonly string ds_CompressionLevelApp = "CompressionLevelApp";

        /// <summary>
        /// True if application has been purchased, false otherwise.
        /// </summary>
        private bool boolAppPurchased;

        /// <summary>
        /// True if application has been rated, false otherwise.
        /// </summary>
        private bool boolAppRated = false;

        /// <summary>
        /// True if parent folder has been selected, false otherwise.
        /// </summary>
        private bool boolParentFolderSelected = false;

        /// <summary>
        /// Compression level to use in compression operation. 
        /// Values are CompressionLevel.NoCompression, CompressionLevel.Fastest, CompressionLevel.Optimal.
        /// </summary>
        CompressionLevel compressionLevelApp = CompressionLevel.NoCompression;

        /// <summary>
        /// This is StorageFolder retrieved from FutureAccessList. User picked this StorageFolder.
        /// Retrieval of this StorageFolder from FutureAccessList insures application has delete access to it.
        /// Save value here so other methods can access it as needed.
        /// </summary>
        private StorageFolder storageFolderToken = null;

        /// <summary>
        /// File archive extension used by application. Current value is ".zip".
        /// </summary>
        private readonly string stringCompressedFileExtension = ".zip";

        /// <summary>
        /// Timer used by ProgressBarHomeShow() to time compression and extraction processes.
        /// </summary>
        private readonly Stopwatch stopWatchTimer = new Stopwatch();

        /// <summary>
        /// Elapsed time from stopWatchTimer. Retrieve this value to get elapsed time.
        /// </summary>
        private TimeSpan timeSpanElapsed = TimeSpan.Zero;

        /// <summary>
        /// Application folder message string used in two locations so set here.
        /// </summary>
        private readonly string stringAppFolderMsg = "Click following button to select parent folder.\nThis will provide application access to folders and files in hierarchy of parent folder.";

        /// <summary>
        /// Select application folder string used in mutiple locations so set here. Current value is "Select folder that contains folders or files to archive or extract.".
        /// </summary>
        private readonly string stringAppFolderSelect = "Select folder that contains folders or files to archive or extract.";

        /// <summary>
        /// Show User ButRateApp button if this number of page loads since last reset.  Current value is 6.
        /// </summary>
        private readonly int intShowButRateApp = 6;

        public MainPage()
        {
            InitializeComponent();
        }

        /*** Private Methods ***************************************************************************************************/

        /// <summary>
        /// Get purchase status of application. Method controls visibility/Enable of PBarStatus, TblkPurchaseApp, and ButPurchaseApp.
        /// </summary>
        private async Task AppPurchaseCheck()
        {
            if (boolAppPurchased)
            {
                // App has been purchased so hide following values and return.
                PBarStatus.Visibility = Visibility.Collapsed;
                TblkPurchaseApp.Visibility = Visibility.Collapsed;
                LibMPC.ButtonVisibility(ButPurchaseApp, false);
            }
            else
            {
                // App has not been purchased so do purchase check.
                LibMPC.OutputMsgBright(TblkPurchaseApp, "Application purchase check in progress...");
                PBarStatus.Foreground = LibMPC.colorError;          // Set color PBarStatus from default.
                PBarStatus.Visibility = Visibility.Visible;
                PBarStatus.IsIndeterminate = true;
                EnablePageItems(false);
                boolAppPurchased = await LibMPC.AppPurchaseStatusAsync(applicationDataContainer, ds_BoolAppPurchased);
                if (boolAppPurchased)
                {
                    LibMPC.OutputMsgSuccess(TblkPurchaseApp, LibMPC.stringAppPurchaseResult);
                    LibMPC.ButtonVisibility(ButPurchaseApp, false);
                }
                else
                {
                    LibMPC.OutputMsgError(TblkPurchaseApp, LibMPC.stringAppPurchaseResult);
                    LibMPC.ButtonVisibility(ButPurchaseApp, true);
                }
                PBarStatus.IsIndeterminate = false;
                PBarStatus.Visibility = Visibility.Collapsed;
                EnablePageItems(true);
            }
        }

        /// <summary>
        /// Attempt to buy application. Method controls visibility/Enable of PBarStatus, TblkPurchaseApp, and ButPurchaseApp.
        /// </summary>
        private async Task AppPurchaseBuy()
        {
            LibMPC.OutputMsgBright(TblkPurchaseApp, "Attempting to purchase application...");
            EnablePageItems(false);
            PBarStatus.Foreground = LibMPC.colorError;          // Set color PBarStatus from default.
            PBarStatus.Visibility = Visibility.Visible;
            PBarStatus.IsIndeterminate = true;
            boolAppPurchased = await LibMPC.AppPurchaseBuyAsync(applicationDataContainer, ds_BoolAppPurchased);
            if (boolAppPurchased)
            {
                // App purchased.
                LibMPC.OutputMsgSuccess(TblkPurchaseApp, LibMPC.stringAppPurchaseResult);
                LibMPC.ButtonVisibility(ButPurchaseApp, false);
            }
            else
            {
                // App not purchased.
                LibMPC.OutputMsgError(TblkPurchaseApp, LibMPC.stringAppPurchaseResult);
                LibMPC.ButtonVisibility(ButPurchaseApp, true);
            }
            PBarStatus.IsIndeterminate = false;
            PBarStatus.Visibility = Visibility.Collapsed;
            EnablePageItems(true);
        }

        /// <summary>
        /// If application has not been rated then show ButRateApp occasionally.
        /// </summary>
        private void AppRatedCheck()
        {
            if (!boolAppRated)
            {
                if (applicationDataContainer.Values.ContainsKey(ds_IntAppRatedCounter))
                {
                    int intAppRatedCounter = (int)applicationDataContainer.Values[ds_IntAppRatedCounter];
                    intAppRatedCounter++;
                    if (intAppRatedCounter >= intShowButRateApp)
                    {
                        // Make ButRateApp visible.
                        //if (boolAppPurchased)         // No margin adjustment needed for this App.
                        //    ButRateApp.Margin = new Thickness(16, 0, 16, 16);    // Change margin from (16, 0, 16 ,16). Order is left, top, right, bottom.
                        applicationDataContainer.Values[ds_IntAppRatedCounter] = 0;     // Reset data store setting to 0.
                        ButRateApp.Foreground = LibMPC.colorSuccess;
                        LibMPC.ButtonVisibility(ButRateApp, true);
                    }
                    else
                        applicationDataContainer.Values[ds_IntAppRatedCounter] = intAppRatedCounter;     // Update data store setting to intAppRatedCounter.
                }
                else
                    applicationDataContainer.Values[ds_IntAppRatedCounter] = 1;     // Initialize data store setting to 1.
            }
        }

        /// <summary>
        /// Enable items on page if boolEnableItems is true, otherwise disable items on page.
        /// </summary>
        /// <param name="boolEnableItems">If true then enable page items, otherwise disable.</param>
        private void EnablePageItems(bool boolEnableItems)
        {
            ButRadioNoCompression.IsEnabled = boolEnableItems;
            ButRadioFastest.IsEnabled = boolEnableItems;
            ButRadioOptimal.IsEnabled = boolEnableItems;
            LibMPC.ButtonIsEnabled(ButAppFolderPick, boolEnableItems);
            LibMPC.ButtonIsEnabled(ButAppFolderOpen, boolEnableItems);
            LibMPC.ButtonIsEnabled(ButArchiveFile, boolEnableItems);
            LibMPC.ButtonIsEnabled(ButArchiveFolder, boolEnableItems);
            LibMPC.ButtonIsEnabled(ButExtractArchiveFile, boolEnableItems);
            LibMPC.ButtonIsEnabled(ButPurchaseApp, boolEnableItems);
            LibMPC.ButtonIsEnabled(ButRateApp, boolEnableItems);
        }

        /// <summary>
        /// On App start enable various buttons.
        /// </summary>
        private void AppStartEnableButtons()
        {
            if (ButPurchaseApp.Visibility == Visibility.Visible)
                LibMPC.ButtonIsEnabled(ButPurchaseApp, true);
            if (ButRateApp.Visibility == Visibility.Visible)
                LibMPC.ButtonIsEnabled(ButRateApp, true);
            LibMPC.ButtonIsEnabled(ButAppFolderPick, true);
            ButAppFolderPick.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Reset App to various states using parameter enumResetApp.
        /// </summary>
        /// <param name="enumResetApp">Enum used to reset App setup values.</param>
        private void AppReset(EnumResetApp enumResetApp)
        {
            switch (enumResetApp)
            {
                case EnumResetApp.DoNothing:                // Do nothing. Most common so exit quick.
                    break;
                case EnumResetApp.ResetApp:                 // Clear all data store settings.
                    applicationDataContainer.Values.Clear();
                    break;
                case EnumResetApp.ResetPurchaseHistory:     // Clear App purchase history.
                    applicationDataContainer.Values.Remove(ds_BoolAppPurchased);
                    boolAppPurchased = false;
                    break;
                case EnumResetApp.ResetRateHistory:         // Clear App rate history.
                    applicationDataContainer.Values.Remove(ds_BoolAppRated);
                    boolAppRated = false;
                    break;
                case EnumResetApp.ShowDataStoreValues:      // Show data store values via Debug.
                    LibMPC.ListDataStoreItems(applicationDataContainer);
                    break;
                default:    // Throw exception so error can be discovered and corrected.
                    throw new NotSupportedException($"MainPage.AppReset(): enumResetApp={enumResetApp} not found in switch statement.");
            }
        }

        /// <summary>
        /// Retrieve or set default compression level to use.
        /// </summary>
        /// <returns></returns>
        private void AppCompressionLevelSet()
        {
            // Next line for testing. It will delete data store value ds_CompressionLevelApp. Comment out on following App run to return to normal operation.
            // applicationDataContainer.Values.Remove(ds_CompressionLevelApp);
            if (applicationDataContainer.Values.ContainsKey(ds_CompressionLevelApp))
            {
                object objectCompressionLevel = applicationDataContainer.Values[ds_CompressionLevelApp];
                switch (objectCompressionLevel)
                {
                    case "Fastest":
                        compressionLevelApp = CompressionLevel.Fastest;
                        ButRadioFastest.IsChecked = true;
                        break;
                    case "Optimal":
                        compressionLevelApp = CompressionLevel.Optimal;
                        ButRadioOptimal.IsChecked = true;
                        break;
                    default:
                        compressionLevelApp = CompressionLevel.NoCompression;
                        applicationDataContainer.Values[ds_CompressionLevelApp] = "NoCompression";      // Save setting to data store.
                        ButRadioNoCompression.IsChecked = true;
                        break;
                }
            }
            else
            {
                compressionLevelApp = CompressionLevel.NoCompression;
                applicationDataContainer.Values[ds_CompressionLevelApp] = "NoCompression";   // Save setting to data store.
                ButRadioNoCompression.IsChecked = true;
                // Debug.WriteLine($"AppCompressionLevelSet(): Created store key ds_CompressionLevelApp and set it to NoCompression");
            }
        }

        /// <summary>
        /// If boolShow is true then show progress bar, reset, and start timer, otherwise hide progress bar and stop timer.
        /// Elapsed time of timer is placed in global variable timeSpanElapsed.
        /// </summary>
        /// <param name="boolShow"></param>
        private void ProgressBarHPShow(bool boolShow)
        {
            if (boolShow)
            {
                stopWatchTimer.Reset();
                stopWatchTimer.Start();             // Start the timer.
                timeSpanElapsed = TimeSpan.Zero;    // Zero in case any access to value before timer stops.
                PBarStatus.Visibility = Visibility.Visible;
                PBarStatus.IsIndeterminate = true;
            }
            else
            {
                PBarStatus.IsIndeterminate = false;
                PBarStatus.Visibility = Visibility.Collapsed;
                stopWatchTimer.Stop();
                timeSpanElapsed = stopWatchTimer.Elapsed;
                // Debug.WriteLine($"ProgressBarHPShow(): PBarStatus ran for {timeSpanElapsed.TotalSeconds:N2} seconds.");
            }
        }

        /// <summary>
        /// Display item processing message and start timer.
        /// </summary>
        /// <param name="iStorageItem">StorageFolder or StorageFile to be processed.</param>
        private void ItemIsBeingProcessedMessage(IStorageItem iStorageItem)
        {
            LibMPC.OutputMsgNormal(TblkOutput, $"Processing {iStorageItem.Path}.");
            ProgressBarHPShow(true);
            // Next statement collpases "Application has been purchased. Thank you!" returned from LibMPC.AppPurchaseTrue().
            // This is kind of needed for this App since one-page app that never navigates to any other pages.
            if (boolAppPurchased)
                TblkPurchaseApp.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Return string made from list of truncated item paths below Locker folder not processed.
        /// </summary>
        /// <param name="listItemPathErrors">List of item paths not processed.</param>
        /// <returns></returns>
        private string StringListItemPathErrors(List<string> listItemPathErrors)
        {
            string stringItemsNotProcessed = string.Empty;
            if (listItemPathErrors.Count > 0)
            {
                if (listItemPathErrors.Count == 1)
                {
                    List<string> list_stringMessageError = new List<string>()
                    {
                        $"{Environment.NewLine}{listItemPathErrors.Count}",   // Variable.
                        "item not processed"        // Singular.
                    };
                    stringItemsNotProcessed = LibMPC.JoinListString(list_stringMessageError, EnumStringSeparator.OneSpace);
                }
                else
                {
                    List<string> list_stringMessageError = new List<string>()
                    {
                        $"{Environment.NewLine}{listItemPathErrors.Count}",   // Variable.
                        "items not processed"       // Plural.
                    };
                    stringItemsNotProcessed = LibMPC.JoinListString(list_stringMessageError, EnumStringSeparator.OneSpace);
                }
                // Create output string listing files not processed.
                foreach (string stringItem in listItemPathErrors)
                    stringItemsNotProcessed += $"{Environment.NewLine}{stringItem}";     // Add item to output string.
            }
            return stringItemsNotProcessed;
        }

        /// <summary>
        /// Returns StorageFolder equivalent to storageFolderPicked if success, null otherwise.
        /// StorageFolder returned by FolderPicker does not allow application to delete picked folder
        /// even if application has access to folder above it. 
        /// This is a programmatic work-a-round to get delete access to picked folder.
        /// This method will return null if picked folder is not in hierarchy of selected parent folder.
        /// </summary>
        /// <param name="storageFolderPicked">StorageFolder obtained from folder picker.</param>
        /// <returns></returns>
        private async Task<StorageFolder> StorageFolderFromFolderPickerAsync(StorageFolder storageFolderPicked)
        {
            try
            {
                // Check if storageFolderPicked has read and write access to parent folder. Continue if so.
                // GetParentAsync() will return null if application not purchased or does not have access to storageFolderPicked.
                StorageFolder storageFolderParent = await storageFolderPicked.GetParentAsync();
                if (storageFolderParent != null)
                {
                    IStorageItem iStorageItem = await storageFolderParent.TryGetItemAsync(storageFolderPicked.Name);
                    if (iStorageItem != null)   // Item found but do not know if was folder or file.
                    {
                        if (iStorageItem.IsOfType(StorageItemTypes.Folder))
                        {
                            // Returned StorageFolder is programmatically equivalent to storageFolderPicked and application can now delete it.
                            return (StorageFolder)iStorageItem;
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
                throw;
            }
        }

        /// <summary>
        /// Check if storageFolderToken exists. User can delete storageFolderToken at any time so need to check if folder exists at start of each process.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> StorageFolderTokenCheckIfExistsAsync()
        {
            try
            {
                // Note: TryGetItemAsync() will not work here since application cannot get parent folder of storageFolderToken.
                // Use following line as alternative and catch FileNotFoundException exception if occurrs.
                await StorageFolder.GetFolderFromPathAsync(storageFolderToken.Path);   // This will throw FileNotFoundException if storageFolderToken not found.
                // Debug.WriteLine("StorageFolderTokenCheckIfExistsAsync(): Returned true since found storageFolderToken");
                return true;
            }
            catch   // (Exception ex)
            {
                // Debug.WriteLine($"StorageFolderTokenCheckIfExistsAsync(): Returned false since unhandled exception occurred. {ex.GetType()}");
                return false;
                throw;
            }
        }

        /// <summary>
        /// Return string to show User if StorageFolderTokenCheckIfExistsAsync() returns false.
        /// This will disable buttons and force User to pick a new parent folder.
        /// </summary>
        private string StorageFolderTokenNotFoundErrorMessage()
        {
            EnablePageItems(false);     // Could not find storageFolderToken so disable butttons preventing user from doing anything until new folder selected.
            AppStartEnableButtons();
            return $"Could not find {storageFolderToken.Path}.  Did you delete, move, or rename this folder while this App was running?";
        }

        /// <summary>
        /// Before archiving folder or file, check that folders do not have an extension and that files have only one extension.
        /// </summary>
        /// <param name="storageItemToCheck">StorageItem to check.</param>
        /// <returns></returns>
        private int GetNumberOfExtensions(IStorageItem storageItemToCheck)
        {
            string stringName1 = Path.GetFileNameWithoutExtension(storageItemToCheck.Name);     // Get filename less one extension, if any.
            if (storageItemToCheck.Name.Equals(stringName1))
            {
                // storageItemToCheck does not have an extension.
                // Debug.WriteLine($"GetNumberOfExtensions(): storageItemToCheck does not have an extension since {storageItemToCheck.Name} equals {stringName1}");
                return 0;
            }
            int intExtensions = 1;  // Must have at least one extension if got this far.
            string stringName2 = Path.GetFileNameWithoutExtension(stringName1);     // Get filename less one extension, if any.
            while (!stringName1.Equals(stringName2))
            {
                intExtensions++;
                // Debug.WriteLine($"GetNumberOfExtensions(): stringName1={stringName1}, stringName2={stringName2}, intExtensions={intExtensions}");
                stringName1 = stringName2;
                stringName2 = Path.GetFileNameWithoutExtension(stringName1);     // Get filename less one extension, if any.
            }
            // Debug.WriteLine($"GetNumberOfExtensions(): Number of extension found is intExtensions={intExtensions}");
            return intExtensions;
        }

        /// <summary>
        /// Open Windows 10 Store App so User can rate and review this App.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> RateAppInW10Store()
        {
            if (await LibMPC.ShowRatingReviewDialogAsync())
            {
                boolAppRated = true;
                applicationDataContainer.Values[ds_BoolAppRated] = true;        // Write setting to data store. 
                applicationDataContainer.Values.Remove(ds_IntAppRatedCounter);  // Remove ds_IntAppRatedCounter since no longer used.
                return true;
            }
            else
                return false;
        }

        /*** Page Events *******************************************************************************************************/

        /// <summary>
        /// Initialize settings for this page.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            // Get application data store location.
            // https://msdn.microsoft.com/windows/uwp/app-settings/store-and-retrieve-app-data#local-app-data
            applicationDataContainer = ApplicationData.Current.LocalSettings;
            LibMPC.CustomizeAppTitleBar();
            LibMPC.OutputMsgNormal(TblkAppFolderMsg, $"Version: {stringAppVersion}{Environment.NewLine}{stringAppFolderMsg}");
            // Hide following buttons until needed.
            LibMPC.ButtonVisibility(ButPurchaseApp, false);
            LibMPC.ButtonVisibility(ButRateApp, false);
            List<Button> listButtonsThisPage = new List<Button>()
            {
                ButAppFolderPick,
                ButAppFolderOpen,
                ButArchiveFile,
                ButArchiveFolder,
                ButExtractArchiveFile,
                ButPurchaseApp
            };
            LibMPC.SizePageButtons(listButtonsThisPage);

            // TODO: set next line to EnumResetApp.DoNothing before store publish.
            AppReset(EnumResetApp.DoNothing);    // Reset App to various states using EnumResetApp.

            AppCompressionLevelSet();   // Do this before next lines so output value is overwritten by next lines.
            LibMPC.OutputMsgNormal(TblkOutput, stringAppFolderSelect);
            // Get data store values for next two items and set to true or false.
            boolAppPurchased = LibMPC.DataStoreStringToBool(applicationDataContainer, ds_BoolAppPurchased);
            boolAppRated = LibMPC.DataStoreStringToBool(applicationDataContainer, ds_BoolAppRated);
            // AppReset(EnumResetApp.ShowDataStoreValues);     // TODO: Comment out this line before store publish. Show data store values.
            await AppPurchaseCheck();
            AppRatedCheck();
            // Note: Do not need to turn scrollviewer on since it was never turned off since this is a MainPage only application.
            // Disable/Enable buttons on App start.
            EnablePageItems(false);
            AppStartEnableButtons();
        }

        /// <summary>
        /// Invoked when User clicks ButAppFolderPick to select folder to use.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButAppFolderPick_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            bool boolSuccess = false;
            string stringMessage;
            EnablePageItems(false);
            LibMPC.OutputMsgNormal(TblkOutput, stringAppFolderSelect);
            FolderPicker folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            // Need at least one filter to prevent exception.
            folderPicker.FileTypeFilter.Add("*");
            StorageFolder storageFolderPicked = await folderPicker.PickSingleFolderAsync();
            if (storageFolderPicked != null)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("tokenStorageFolderPicked", storageFolderPicked);
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem("tokenStorageFolderPicked"))
                {
                    storageFolderToken = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("tokenStorageFolderPicked");
                    if (storageFolderToken != null)
                    {
                        stringMessage = $"Current parent folder is: {storageFolderToken.Path}";
                        // Debug.WriteLine($"ButAppFolderPick_Click(): Continue since storageFolderToken not null, Path={storageFolderToken.Path}");
                        TblkAppFolderMsg.Text = $"{stringAppFolderMsg}{Environment.NewLine}{stringMessage}";
                        boolSuccess = true;
                    }
                    else
                        stringMessage = $"Aborted since could not retrieve selected App folder {storageFolderPicked.Path}.";
                }
                else
                    stringMessage = $"Aborted since could not access selected App folder {storageFolderPicked.Path}.";
            }
            else
                stringMessage = "Aborted since did not select a folder.";
            if(boolSuccess)
            {
                LibMPC.OutputMsgSuccess(TblkOutput, stringMessage);
                boolParentFolderSelected = true;
                EnablePageItems(true);
                ButAppFolderOpen.Focus(FocusState.Programmatic);
            }
            else
            {
                LibMPC.OutputMsgError(TblkOutput, stringMessage);
                AppStartEnableButtons();
            }
        }

        /// <summary>
        /// Invoked when User clicks ButAppFolderOpen which launches MS FileExplorer to selected folder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButAppFolderOpen_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            EnablePageItems(false);
            if (await StorageFolderTokenCheckIfExistsAsync())     // Check if storageFolderToken exists.
            {
                await LibMPC.LaunchFileExplorerAsync(storageFolderToken);
                LibMPC.OutputMsgSuccess(TblkOutput, $"File Explorer opened to {storageFolderToken.Path}.");
            }
            else
                LibMPC.OutputMsgError(TblkOutput, StorageFolderTokenNotFoundErrorMessage());
            EnablePageItems(true);
        }

        /// <summary>
        /// RadioButton event NoCompression.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButRadioNoCompression_Checked(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            compressionLevelApp = CompressionLevel.NoCompression;
            applicationDataContainer.Values[ds_CompressionLevelApp] = "NoCompression";   // Save setting to data store.
            LibMPC.OutputMsgNormal(TblkOutput, "Archived folders and files will not be compressed.");
        }

        /// <summary>
        /// RadioButton event Fastest.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButRadioFastest_Checked(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            compressionLevelApp = CompressionLevel.Fastest;
            applicationDataContainer.Values[ds_CompressionLevelApp] = "Fastest";        // Save setting to data store.
            LibMPC.OutputMsgNormal(TblkOutput, "Archived folders and files will be compressed using fast method.");
        }

        /// <summary>
        /// RadioButton event Optimal.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButRadioOptimal_Checked(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            compressionLevelApp = CompressionLevel.Optimal;
            applicationDataContainer.Values[ds_CompressionLevelApp] = "Optimal";        // Save setting to data store.
            LibMPC.OutputMsgNormal(TblkOutput, "Archived folders and files will be compressed using optimal method.");
        }

        /// <summary>
        /// Archive file picked by User.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButArchiveFile_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            bool boolSuccess = false;
            string stringMessage;
            EnablePageItems(false);
            if (await StorageFolderTokenCheckIfExistsAsync())     // Check if storageFolderToken exists.
            {
                LibMPC.OutputMsgNormal(TblkOutput, $"Pick file to archive in hierarchy of parent folder.");
                FileOpenPicker fileOpenPicker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    ViewMode = PickerViewMode.List
                };
                // Need at least one filter to prevent exception.
                fileOpenPicker.FileTypeFilter.Add("*");
                StorageFile storageFilePicked = await fileOpenPicker.PickSingleFileAsync();
                if (storageFilePicked != null)
                {
                    // GetParentAsync() in next line will return null if application does not have access to it.
                    StorageFolder storageFolderParent = await storageFilePicked.GetParentAsync();
                    if (storageFolderParent != null)
                    {
                        if (!stringCompressedFileExtension.Equals(Path.GetExtension(storageFilePicked.Name)))
                        {
                            if (GetNumberOfExtensions(storageFilePicked).Equals(1))
                            {
                                // Check if storageFilePicked is locked. Abort if so.
                                if (!await LibZA.IStorageItemLockCheckAsync(storageFilePicked))
                                {
                                    // Check that destination item does not exist before creating it.
                                    string stringNameDestination = $"{storageFilePicked.Name}{stringCompressedFileExtension}";
                                    // Debug.WriteLine($"ButArchiveFile_Click(): stringNameDestination={stringNameDestination}");
                                    if (await storageFolderParent.TryGetItemAsync(stringNameDestination) == null)
                                    {
                                        // Create empty archive file.
                                        StorageFile storageFileArchive = await storageFolderParent.CreateFileAsync(stringNameDestination);
                                        if (storageFileArchive != null)
                                        {
                                            ItemIsBeingProcessedMessage(storageFilePicked);
                                            // Lock check done by this method so set boolCheckIfLocked=false in next line.
                                            if (await LibZA.ZipArchiveCompressAsync(storageFileArchive, storageFilePicked, compressionLevelApp, false))
                                            {
                                                ProgressBarHPShow(false);
                                                stringMessage = $"Archived {storageFilePicked.Name} to {storageFileArchive.Path} ({timeSpanElapsed.TotalSeconds:N2} seconds).";
                                                await storageFilePicked.DeleteAsync(StorageDeleteOption.PermanentDelete);     // Cleanup by deleting storageFilePicked since it was archived.
                                                boolSuccess = true;
                                            }
                                            else
                                            {
                                                ProgressBarHPShow(false);
                                                stringMessage = $"Aborted since could not archive {storageFilePicked.Name} to {storageFileArchive.Path} ({timeSpanElapsed.TotalSeconds:N2} seconds).";
                                                await storageFileArchive.DeleteAsync(StorageDeleteOption.PermanentDelete);     // Cleanup by deleting storageFileArchive since could not archive storageFilePicked to it.
                                            }
                                        }
                                        else
                                            stringMessage = $"Aborted since could not create archive file {stringNameDestination} in {storageFolderParent.Path}.";
                                    }
                                    else
                                        stringMessage = $"Aborted since destination file {stringNameDestination} exists in {storageFolderParent.Path}. Move or delete existing destination file and try again.";
                                }
                                else
                                    stringMessage = $"Aborted since {storageFilePicked.Name} is locked.  Another application may be using file.";
                            }
                            else
                                stringMessage = $"Aborted since {storageFilePicked.Name} does not have exactly one file extension.";
                        }
                        else
                            stringMessage = $"Aborted since {storageFilePicked.Name} is already archived.";
                    }
                    else
                        stringMessage = $"Aborted since could not get access to parent folder of {storageFilePicked.Path}.";
                }
                else
                    stringMessage = "Aborted since did not select a file.";
            }
            else
                stringMessage = StorageFolderTokenNotFoundErrorMessage();
            if (boolSuccess)
                LibMPC.OutputMsgSuccess(TblkOutput, stringMessage);
            else
                LibMPC.OutputMsgError(TblkOutput, stringMessage);
            EnablePageItems(true);
        }

        /// <summary>
        /// Archive folder picked by User.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButArchiveFolder_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            bool boolSuccess = false;
            string stringMessage;
            EnablePageItems(false);
            if (await StorageFolderTokenCheckIfExistsAsync())     // Check if storageFolderToken exists.
            {
                LibMPC.OutputMsgNormal(TblkOutput, $"Pick folder to archive in hierarchy of parent folder.");
                FolderPicker folderPicker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    ViewMode = PickerViewMode.List
                };
                // Need at least one filter to prevent exception.
                folderPicker.FileTypeFilter.Add("*");
                StorageFolder storageFolderPicked = await folderPicker.PickSingleFolderAsync();
                if (storageFolderPicked != null)
                {
                    // Check if storageFolderSelected is in previously selected folder. Continue if so.
                    StorageFolder storageFolderSelected = await StorageFolderFromFolderPickerAsync(storageFolderPicked);
                    if (storageFolderSelected != null)
                    {
                        // Do not need to check if storageFolderSelected has extension of ".zip" since it would be a file and FolderPicker only allows selection of folders.
                        if (GetNumberOfExtensions(storageFolderSelected).Equals(0))
                        {
                            //Check if storageFolderPicked is locked.Abort if so.
                            if (!await LibZA.IStorageItemLockCheckAsync(storageFolderSelected))
                            {
                                StorageFolder storageFolderParent = await storageFolderSelected.GetParentAsync();
                                if (storageFolderParent != null)
                                {
                                    // Check that destination item does not exist before creating it.
                                    string stringNameDestination = $"{storageFolderSelected.Name}{stringCompressedFileExtension}";
                                    // Debug.WriteLine($"ButArchiveFolder_Click(): stringNameDestination={stringNameDestination}");
                                    if (await storageFolderParent.TryGetItemAsync(stringNameDestination) == null)
                                    {
                                        // Debug.WriteLine($"ButArchiveFolder_Click(): Did not find file stringNameDestination={stringNameDestination} in {storageFolderParent.Path}");
                                        // Create empty archive file.
                                        StorageFile storageFileArchive = await storageFolderParent.CreateFileAsync(stringNameDestination);
                                        if (storageFileArchive != null)
                                        {
                                            // Debug.WriteLine($"ButArchiveFolder_Click(): Created storageFileArchive.Path={storageFileArchive.Path}");
                                            ItemIsBeingProcessedMessage(storageFolderSelected);
                                            // Lock check done by this method so set boolCheckIfLocked=false in next line.
                                            if (await LibZA.ZipArchiveCompressAsync(storageFileArchive, storageFolderSelected, compressionLevelApp, false))
                                            {
                                                ProgressBarHPShow(false);
                                                stringMessage = $"Archived {storageFolderSelected.Name} to {storageFileArchive.Path} ({timeSpanElapsed.TotalSeconds:N2} seconds).";
                                                await storageFolderSelected.DeleteAsync(StorageDeleteOption.PermanentDelete);     // Cleanup by deleting storageFolderSelected since it was archived.
                                                boolSuccess = true;
                                            }
                                            else
                                            {
                                                ProgressBarHPShow(false);
                                                // At least one entry not compressed into archive file.
                                                stringMessage = $"Could not archive all items in {storageFolderSelected.Name} to {storageFileArchive.Path} ({ timeSpanElapsed.TotalSeconds:N2} seconds).\nItems not compressed:\n{StringListItemPathErrors(LibZA.listItemPathErrors)}";
                                            }
                                        }
                                        else
                                            stringMessage = $"Aborted since could not create archive file {stringNameDestination} in {storageFolderParent.Path}.";
                                    }
                                    else
                                        stringMessage = $"Aborted since destination file {stringNameDestination} exists in {storageFolderParent.Path}. Move or delete existing destination file and try again.";
                                }
                                else
                                    stringMessage = $"Aborted since could not get access to parent folder of {storageFolderSelected.Path}.";
                            }
                            else
                                stringMessage = $"Aborted since {storageFolderSelected.Name} is locked.  Another application may be using a file in the folder.";
                        }
                        else
                            stringMessage = $"Aborted since {storageFolderSelected.Name} has extension.  Rename folder to remove extension and try again.";
                    }
                    else
                        stringMessage = $"Aborted since could not get access to {storageFolderPicked.Path}.";
                }
                else
                    stringMessage = "Aborted since did not select a folder.";
            }
            else
                stringMessage = StorageFolderTokenNotFoundErrorMessage();
            if (boolSuccess)
                LibMPC.OutputMsgSuccess(TblkOutput, stringMessage);
            else
                LibMPC.OutputMsgError(TblkOutput, stringMessage);
            EnablePageItems(true);
        }

        /// <summary>
        /// Extract archived file picked by User.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButExtractArchiveFile_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            bool boolSuccess = false;
            string stringMessage;
            EnablePageItems(false);
            if (await StorageFolderTokenCheckIfExistsAsync())     // Check if storageFolderToken exists.
            {
                LibMPC.OutputMsgNormal(TblkOutput, $"Pick archived file to extract that has extension of {stringCompressedFileExtension} in hierarchy of parent folder.");
                FileOpenPicker fileOpenPicker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    ViewMode = PickerViewMode.List
                };
                // Need at least one filter to prevent exception.
                fileOpenPicker.FileTypeFilter.Add(stringCompressedFileExtension);   // Cannot pick any files that do not have matching extension.
                StorageFile storageFilePicked = await fileOpenPicker.PickSingleFileAsync();
                if (storageFilePicked != null)
                {
                    // GetParentAsync() in next line will return null if application does not have access to it.
                    StorageFolder storageFolderParent = await storageFilePicked.GetParentAsync();
                    if (storageFolderParent != null)
                    {
                        // Check if storageFilePicked is locked. Abort if so.
                        if (!await LibZA.IStorageItemLockCheckAsync(storageFilePicked))
                        {
                            // If storageFilePicked has one file extension then it was originally a folder so extract back to a new folder created in parent folder.
                            // If storageFilePicked has two file extensions then it was originally a file so extact to parent folder.
                            string stringDestinationName1 = Path.GetFileNameWithoutExtension(storageFilePicked.Name);       // Get destination name less extension.
                            string stringDestinationName2 = Path.GetFileNameWithoutExtension(stringDestinationName1);       // Get destination name less extension.

                            // Debug.WriteLine($"ButExtractArchiveFile_Click(): stringDestinationName1={stringDestinationName1}, stringDestinationName2={ stringDestinationName2}");

                            if (stringDestinationName1.Equals(stringDestinationName2))
                            {
                                // Destination names are same so storageFilePicked had only one extension indicating it was originally a folder.
                                // Therefore, extract storageFilePicked to a new folder created in storageFolderParent.
                                if (await storageFolderParent.TryGetItemAsync(stringDestinationName1) == null)
                                {
                                    StorageFolder storageFolderDestination = await storageFolderParent.CreateFolderAsync(stringDestinationName1);
                                    if (storageFolderDestination != null)
                                    {
                                        ItemIsBeingProcessedMessage(storageFilePicked);
                                        // Lock check done by this method so set boolCheckIfLocked=false in next line.
                                        if (await LibZA.ZipArchiveExtractAsync(storageFilePicked, storageFolderDestination, false))
                                        {
                                            ProgressBarHPShow(false);
                                            // Folder extracted without error.
                                            stringMessage = $"Extracted {storageFilePicked.Name} to {storageFolderDestination.Path} ({timeSpanElapsed.TotalSeconds:N2} seconds).";
                                            await storageFilePicked.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                            boolSuccess = true;
                                        }
                                        else
                                        {
                                            ProgressBarHPShow(false);
                                            // At least one entry not extracted.
                                            stringMessage = $"Could not extract all items in {storageFilePicked.Name} to {storageFolderDestination.Path} ({timeSpanElapsed.TotalSeconds:N2} seconds).\nItems not extracted:\n{StringListItemPathErrors(LibZA.listItemPathErrors)}";
                                        }
                                    }
                                    else
                                        stringMessage = $"Aborted since could not create destination folder {stringDestinationName1}.";
                                }
                                else
                                    stringMessage = $"Aborted since destination folder {stringDestinationName1} exists in {storageFolderParent.Path}. Move or delete existing destination folder and try again.";
                            }
                            else
                            {
                                // Destination names not equal so storageFilePicked had two extensions indicating it was orginally a file.
                                // Therefore, extract storageFilePicked to storageFolderParent.
                                if (await storageFolderParent.TryGetItemAsync(stringDestinationName1) == null)
                                {
                                    ItemIsBeingProcessedMessage(storageFilePicked);
                                    if (await LibZA.ZipArchiveExtractAsync(storageFilePicked, storageFolderParent))
                                    {
                                        ProgressBarHPShow(false);
                                        // File extracted without error.
                                        stringMessage = $"Extracted {storageFilePicked.Name} to {storageFolderParent.Path}\\{stringDestinationName1} ({timeSpanElapsed.TotalSeconds:N2} seconds).";
                                        await storageFilePicked.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                        boolSuccess = true;
                                    }
                                    else
                                    {
                                        ProgressBarHPShow(false);
                                        // Could not extract storageFilePicked to storageFolderParent.
                                        stringMessage = $"Aborted since could not extract {storageFilePicked.Name} to {storageFolderParent.Path} ({timeSpanElapsed.TotalSeconds:N2} seconds).";
                                    }
                                }
                                else
                                    stringMessage = $"Aborted since destination file {stringDestinationName1} exists in {storageFolderParent.Path}. Move or delete existing destination file and try again.";
                            }
                        }
                        else
                            stringMessage = $"Aborted since {storageFilePicked.Name} is locked.  Another application may be using file.";
                    }
                    else
                        stringMessage = $"Aborted since could not get access to parent folder of {storageFilePicked.Path}.";
                }
                else
                    stringMessage = "Aborted since did not select a file.";
            }
            else
                stringMessage = StorageFolderTokenNotFoundErrorMessage();
            if (boolSuccess)
                LibMPC.OutputMsgSuccess(TblkOutput, stringMessage);
            else
                LibMPC.OutputMsgError(TblkOutput, stringMessage);
            EnablePageItems(true);
        }

        /// <summary>
        /// Invoked when User clicks ButPurchaseApp. Button is visible if App not purchased, hidden otherwise.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButPurchaseApp_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            await AppPurchaseBuy();
            if (!boolParentFolderSelected)
            {
                EnablePageItems(false);
                AppStartEnableButtons();
            }
        }

        /// <summary>
        /// Invoked when user clicks ButRateApp. MS Store popup box will lock out all access to App.
        /// Goal is to get more App ratings in Microsoft Store without hassling User too much.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButRateApp_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;     // Discard unused parameter.
            _ = e;          // Discard unused parameter.
            if (await RateAppInW10Store())
                LibMPC.ButtonVisibility(ButRateApp, false);
            else
            {
                if (!boolParentFolderSelected)
                {
                    EnablePageItems(false);
                    AppStartEnableButtons();
                }
            }
        }

    }
}
