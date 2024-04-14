﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exporting;
using DiscordChatExporter.Core.Exporting.Filtering;
using DiscordChatExporter.Core.Exporting.Partitioning;
using DiscordChatExporter.Core.Utils.Extensions;
using DiscordChatExporter.Gui.Framework;
using DiscordChatExporter.Gui.Services;

namespace DiscordChatExporter.Gui.ViewModels.Dialogs;

public partial class ExportSetupViewModel : DialogViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly SettingsService _settingsService;

    public Guild? Guild { get; set; }

    public IReadOnlyList<Channel>? Channels { get; set; }

    public bool IsSingleChannel => Channels?.Count == 1;

    public string? OutputPath { get; set; }

    public IReadOnlyList<ExportFormat> AvailableFormats { get; } = Enum.GetValues<ExportFormat>();

    public ExportFormat SelectedFormat { get; set; }

    // This date/time abomination is required because we use separate controls to set these

    public DateTimeOffset? AfterDate { get; set; }

    public bool IsAfterDateSet => AfterDate is not null;

    public TimeSpan? AfterTime { get; set; }

    public DateTimeOffset? After => AfterDate?.Add(AfterTime ?? TimeSpan.Zero);

    public DateTimeOffset? BeforeDate { get; set; }

    public bool IsBeforeDateSet => BeforeDate is not null;

    public TimeSpan? BeforeTime { get; set; }

    public DateTimeOffset? Before => BeforeDate?.Add(BeforeTime ?? TimeSpan.Zero);

    public string? PartitionLimitValue { get; set; }

    public PartitionLimit PartitionLimit =>
        !string.IsNullOrWhiteSpace(PartitionLimitValue)
            ? PartitionLimit.Parse(PartitionLimitValue)
            : PartitionLimit.Null;

    public string? MessageFilterValue { get; set; }

    public MessageFilter MessageFilter =>
        !string.IsNullOrWhiteSpace(MessageFilterValue)
            ? MessageFilter.Parse(MessageFilterValue)
            : MessageFilter.Null;

    public bool ShouldFormatMarkdown { get; set; }

    public bool ShouldDownloadAssets { get; set; }

    public bool ShouldReuseAssets { get; set; }

    public string? AssetsDirPath { get; set; }

    public bool IsAdvancedSectionDisplayed { get; set; }

    public ExportSetupViewModel(DialogManager dialogManager, SettingsService settingsService)
    {
        _dialogManager = dialogManager;
        _settingsService = settingsService;

        // Persist preferences
        SelectedFormat = _settingsService.LastExportFormat;
        PartitionLimitValue = _settingsService.LastPartitionLimitValue;
        MessageFilterValue = _settingsService.LastMessageFilterValue;
        ShouldFormatMarkdown = _settingsService.LastShouldFormatMarkdown;
        ShouldDownloadAssets = _settingsService.LastShouldDownloadAssets;
        ShouldReuseAssets = _settingsService.LastShouldReuseAssets;
        AssetsDirPath = _settingsService.LastAssetsDirPath;

        // Show the "advanced options" section by default if any
        // of the advanced options are set to non-default values.
        IsAdvancedSectionDisplayed =
            After is not null
            || Before is not null
            || !string.IsNullOrWhiteSpace(PartitionLimitValue)
            || !string.IsNullOrWhiteSpace(MessageFilterValue)
            || ShouldDownloadAssets
            || ShouldReuseAssets
            || !string.IsNullOrWhiteSpace(AssetsDirPath);
    }

    [RelayCommand]
    private void ToggleAdvancedSection() =>
        IsAdvancedSectionDisplayed = !IsAdvancedSectionDisplayed;

    [RelayCommand]
    private async Task ShowOutputPathPromptAsync()
    {
        if (IsSingleChannel)
        {
            var defaultFileName = ExportRequest.GetDefaultOutputFileName(
                Guild!,
                Channels!.Single(),
                SelectedFormat,
                After?.Pipe(Snowflake.FromDate),
                Before?.Pipe(Snowflake.FromDate)
            );

            var extension = SelectedFormat.GetFileExtension();

            var path = await _dialogManager.PromptSaveFilePathAsync(
                [
                    new FilePickerFileType($"{extension.ToUpperInvariant()} file")
                    {
                        Patterns = [$"*.{extension}"]
                    }
                ],
                defaultFileName
            );
            if (!string.IsNullOrWhiteSpace(path))
                OutputPath = path;
        }
        else
        {
            var path = await _dialogManager.PromptDirectoryPathAsync();
            if (!string.IsNullOrWhiteSpace(path))
                OutputPath = path;
        }
    }

    [RelayCommand]
    private async Task ShowAssetsDirPathPromptAsync()
    {
        var path = await _dialogManager.PromptDirectoryPathAsync();
        if (!string.IsNullOrWhiteSpace(path))
            AssetsDirPath = path;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        // Prompt the output path if it's not set yet
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            await ShowOutputPathPromptAsync();

            // If the output path is still not set, cancel the export
            if (string.IsNullOrWhiteSpace(OutputPath))
                return;
        }

        // Persist preferences
        _settingsService.LastExportFormat = SelectedFormat;
        _settingsService.LastPartitionLimitValue = PartitionLimitValue;
        _settingsService.LastMessageFilterValue = MessageFilterValue;
        _settingsService.LastShouldFormatMarkdown = ShouldFormatMarkdown;
        _settingsService.LastShouldDownloadAssets = ShouldDownloadAssets;
        _settingsService.LastShouldReuseAssets = ShouldReuseAssets;
        _settingsService.LastAssetsDirPath = AssetsDirPath;

        Close(true);
    }
}
