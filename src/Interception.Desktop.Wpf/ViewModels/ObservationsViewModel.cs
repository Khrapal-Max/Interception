using Interception.Application.Analytics.Dtos;
using Interception.Application.Import.Abstractions;
using Interception.Application.Interceptions.Abstractions;
using Interception.Application.Interceptions.Dtos;
using Interception.Application.Interceptions.TextBlock;
using Interception.Application.Registry.Abstractions;
using Interception.Common.Extensions;
using Interception.Desktop.Wpf.Infrastructure;
using Interception.Domain.Entities;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace Interception.Desktop.Wpf.ViewModels;

/// <summary>
/// Модель представлення реєстру спостережень для WPF.
/// Працює поверх application-сервісів, а не локальних тестових даних.
/// </summary>
public sealed class ObservationsViewModel : ViewModelBase
{
    private const int PageSize = 25;

    private readonly IInterceptionQueryService _queryService;
    private readonly IInterceptionCommandService _commandService;
    private readonly IInterceptionSuggestionService _suggestionService;
    private readonly IInterceptionImportService _importService;
    private readonly IInterceptionActionService _actionService;

    private readonly ObservableCollection<ObservationRecord> _pagedItems = [];
    private readonly ObservableCollection<InterceptionAction> _actions = [];
    private readonly ObservableCollection<FrequencySuggestionDto> _frequencySuggestions = [];
    private readonly ObservableCollection<string> _vectorSuggestions = [];
    private readonly ObservableCollection<ParticipantDraft> _participantItems = [];
    private readonly ObservableCollection<ParticipantSuggestionDto> _participantSuggestions = [];

    private readonly RelayCommand _editSelectedCommand;
    private readonly RelayCommand _deleteSelectedCommand;
    private readonly RelayCommand _prevPageCommand;
    private readonly RelayCommand _nextPageCommand;
    private readonly RelayCommand _applyParticipantSuggestionCommand;

    private ObservationRecord? _selectedObservation;
    private ParticipantDraft? _selectedParticipant;
    private ParticipantSuggestionDto? _selectedParticipantSuggestion;
    private FrequencySuggestionDto? _selectedFrequencySuggestion;
    private string? _selectedVectorSuggestion;
    private InterceptionAction? _selectedAction;

    private int _currentPage = 1;
    private int _totalCount;
    private bool _loading;

    private DateTime? _filterDateFrom;
    private DateTime? _filterDateTo;
    private string? _filterFrequency;
    private string? _filterVectorSignal;
    private string? _filterParticipant;
    private string? _filterLabel;

    private bool _isFormOpen;
    private bool _isFilterOpen;
    private bool _isImportOpen;
    private bool _isTextBlockOpen;

    private Guid? _editingId;
    private DateTime _formObservedDate = DateTime.Now;
    private string _formObservedTime = DateTime.Now.ToString("HH:mm");
    private string? _formFrequency;
    private string? _formDivision;
    private string? _formPointSignal;
    private string? _formVectorSignal;
    private string? _formLabels;
    private string? _formNote;
    private string? _formStatus;

    private string? _importFilePath;
    private string? _importStatus;

    private string? _rawTextBlock;
    private string? _textBlockStatus;

    private string? _serviceStatus;

    public ObservationsViewModel(
         IInterceptionQueryService queryService,
         IInterceptionCommandService commandService,
         IInterceptionSuggestionService suggestionService,
         IInterceptionImportService importService,
         IInterceptionActionService actionService)
    {
        _queryService = queryService;
        _commandService = commandService;
        _suggestionService = suggestionService;
        _importService = importService;
        _actionService = actionService;

        OpenCreateCommand = new RelayCommand(OpenCreate);
        _editSelectedCommand = new RelayCommand(() => _ = OpenEditSelectedAsync(), () => SelectedObservation is not null && CanUseDataServices);
        _deleteSelectedCommand = new RelayCommand(() => _ = DeleteSelectedAsync(), () => SelectedObservation is not null && CanUseDataServices);

        ToggleFilterCommand = new RelayCommand(() => ToggleModal(ModalMode.Filter));
        ToggleImportCommand = new RelayCommand(() => ToggleModal(ModalMode.Import));
        ToggleTextBlockCommand = new RelayCommand(() => ToggleModal(ModalMode.Text));

        ApplyFilterCommand = new RelayCommand(() => _ = ApplyFilterAsync(), () => CanUseDataServices);
        ResetFilterCommand = new RelayCommand(() => _ = ResetFilterAsync(), () => CanUseDataServices);
        SaveObservationCommand = new RelayCommand(() => _ = SaveObservationAsync());
        BrowseImportFileCommand = new RelayCommand(BrowseImportFile);
        RunImportCommand = new RelayCommand(() => _ = RunImportAsync(), () => CanUseDataServices);
        ParseTextBlockCommand = new RelayCommand(() => _ = ParseTextBlockAsync());
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false);
        CloseFilterCommand = new RelayCommand(() => IsFilterOpen = false);
        CloseImportCommand = new RelayCommand(() => IsImportOpen = false);
        CloseTextBlockCommand = new RelayCommand(() => IsTextBlockOpen = false);
        AddParticipantCommand = new RelayCommand(AddParticipant);

        _prevPageCommand = new RelayCommand(() => _ = GoToPageAsync(CurrentPage - 1), () => CurrentPage > 1 && CanUseDataServices);
        _nextPageCommand = new RelayCommand(() => _ = GoToPageAsync(CurrentPage + 1), () => CurrentPage < TotalPages && CanUseDataServices);
        _applyParticipantSuggestionCommand = new RelayCommand(ApplySelectedParticipantSuggestion, () => SelectedParticipant is not null && SelectedParticipantSuggestion is not null);

        EnsureAtLeastOneParticipant();
        _ = InitializeAsync();
    }

    public static string Title => "Реєстр перехоплень";
    public ObservableCollection<ObservationRecord> PagedItems => _pagedItems;
    public ObservableCollection<InterceptionAction> Actions => _actions;
    public ObservableCollection<FrequencySuggestionDto> FrequencySuggestions => _frequencySuggestions;
    public ObservableCollection<string> VectorSuggestions => _vectorSuggestions;
    public ObservableCollection<ParticipantDraft> ParticipantItems => _participantItems;
    public ObservableCollection<ParticipantSuggestionDto> ParticipantSuggestions => _participantSuggestions;

    public static bool CanUseDataServices => true;

    public bool HasServiceStatus => !string.IsNullOrWhiteSpace(ServiceStatus);
    public bool HasFormStatus => !string.IsNullOrWhiteSpace(FormStatus);
    public bool HasImportStatus => !string.IsNullOrWhiteSpace(ImportStatus);
    public bool HasTextBlockStatus => !string.IsNullOrWhiteSpace(TextBlockStatus);
    public bool HasFrequencySuggestions => FrequencySuggestions.Count > 0;
    public bool HasVectorSuggestions => VectorSuggestions.Count > 0;
    public bool HasParticipantSuggestions => ParticipantSuggestions.Count > 0;

    public ObservationRecord? SelectedObservation
    {
        get => _selectedObservation;
        set
        {
            if (!SetProperty(ref _selectedObservation, value))
                return;

            _editSelectedCommand.RaiseCanExecuteChanged();
            _deleteSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public ParticipantDraft? SelectedParticipant
    {
        get => _selectedParticipant;
        set
        {
            if (!SetProperty(ref _selectedParticipant, value))
                return;

            _ = LoadParticipantSuggestionsAsync(value);
            _applyParticipantSuggestionCommand.RaiseCanExecuteChanged();
        }
    }

    public ParticipantSuggestionDto? SelectedParticipantSuggestion
    {
        get => _selectedParticipantSuggestion;
        set
        {
            if (SetProperty(ref _selectedParticipantSuggestion, value))
                _applyParticipantSuggestionCommand.RaiseCanExecuteChanged();
        }
    }

    public FrequencySuggestionDto? SelectedFrequencySuggestion
    {
        get => _selectedFrequencySuggestion;
        set
        {
            if (!SetProperty(ref _selectedFrequencySuggestion, value) || value is null)
                return;

            ApplyFrequencySuggestion(value);
        }
    }

    public string? SelectedVectorSuggestion
    {
        get => _selectedVectorSuggestion;
        set
        {
            if (!SetProperty(ref _selectedVectorSuggestion, value) || string.IsNullOrWhiteSpace(value))
                return;

            ApplyVectorSuggestion(value);
        }
    }

    public InterceptionAction? SelectedAction
    {
        get => _selectedAction;
        set => SetProperty(ref _selectedAction, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
                OnPropertyChanged(nameof(PageSummary));
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
            {
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageSummary));
            }
        }
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public string PageSummary => Loading ? "Завантаження…" : $"Сторінка {CurrentPage} з {TotalPages}. Записів: {TotalCount}";

    public bool Loading
    {
        get => _loading;
        private set
        {
            if (SetProperty(ref _loading, value))
                OnPropertyChanged(nameof(PageSummary));
        }
    }

    public bool HasActiveFilter =>
        FilterDateFrom.HasValue ||
        FilterDateTo.HasValue ||
        !string.IsNullOrWhiteSpace(FilterFrequency) ||
        !string.IsNullOrWhiteSpace(FilterVectorSignal) ||
        !string.IsNullOrWhiteSpace(FilterParticipant) ||
        !string.IsNullOrWhiteSpace(FilterLabel);

    public bool IsAnyModalOpen => IsFormOpen || IsFilterOpen || IsImportOpen || IsTextBlockOpen;

    public DateTime? FilterDateFrom
    {
        get => _filterDateFrom;
        set
        {
            if (SetProperty(ref _filterDateFrom, value))
                OnPropertyChanged(nameof(HasActiveFilter));
        }
    }

    public DateTime? FilterDateTo
    {
        get => _filterDateTo;
        set
        {
            if (SetProperty(ref _filterDateTo, value))
                OnPropertyChanged(nameof(HasActiveFilter));
        }
    }

    public string? FilterFrequency
    {
        get => _filterFrequency;
        set
        {
            if (SetProperty(ref _filterFrequency, value))
                OnPropertyChanged(nameof(HasActiveFilter));
        }
    }

    public string? FilterVectorSignal
    {
        get => _filterVectorSignal;
        set
        {
            if (SetProperty(ref _filterVectorSignal, value))
                OnPropertyChanged(nameof(HasActiveFilter));
        }
    }

    public string? FilterParticipant
    {
        get => _filterParticipant;
        set
        {
            if (SetProperty(ref _filterParticipant, value))
                OnPropertyChanged(nameof(HasActiveFilter));
        }
    }

    public string? FilterLabel
    {
        get => _filterLabel;
        set
        {
            if (SetProperty(ref _filterLabel, value))
                OnPropertyChanged(nameof(HasActiveFilter));
        }
    }

    public bool IsFormOpen
    {
        get => _isFormOpen;
        set
        {
            if (SetProperty(ref _isFormOpen, value))
                OnPropertyChanged(nameof(IsAnyModalOpen));
        }
    }

    public bool IsFilterOpen
    {
        get => _isFilterOpen;
        set
        {
            if (SetProperty(ref _isFilterOpen, value))
                OnPropertyChanged(nameof(IsAnyModalOpen));
        }
    }

    public bool IsImportOpen
    {
        get => _isImportOpen;
        set
        {
            if (SetProperty(ref _isImportOpen, value))
                OnPropertyChanged(nameof(IsAnyModalOpen));
        }
    }

    public bool IsTextBlockOpen
    {
        get => _isTextBlockOpen;
        set
        {
            if (SetProperty(ref _isTextBlockOpen, value))
                OnPropertyChanged(nameof(IsAnyModalOpen));
        }
    }

    public string FormMode => _editingId.HasValue ? "Редагування перехоплення" : "Нове перехоплення";

    public DateTime FormObservedDate
    {
        get => _formObservedDate;
        set => SetProperty(ref _formObservedDate, value);
    }

    public string FormObservedTime
    {
        get => _formObservedTime;
        set => SetProperty(ref _formObservedTime, value);
    }

    public string? FormFrequency
    {
        get => _formFrequency;
        set
        {
            if (!SetProperty(ref _formFrequency, value))
                return;

            _ = RefreshFrequencySuggestionsAsync(value);
            _ = RefreshVectorSuggestionsAsync(FormVectorSignal);
        }
    }

    public string? FormDivision
    {
        get => _formDivision;
        set => SetProperty(ref _formDivision, value);
    }

    public string? FormPointSignal
    {
        get => _formPointSignal;
        set => SetProperty(ref _formPointSignal, value);
    }

    public string? FormVectorSignal
    {
        get => _formVectorSignal;
        set
        {
            if (!SetProperty(ref _formVectorSignal, value))
                return;

            _ = RefreshVectorSuggestionsAsync(value);
        }
    }

    public string? FormLabels
    {
        get => _formLabels;
        set => SetProperty(ref _formLabels, value);
    }

    public string? FormNote
    {
        get => _formNote;
        set => SetProperty(ref _formNote, value);
    }

    public string? FormStatus
    {
        get => _formStatus;
        set
        {
            if (SetProperty(ref _formStatus, value))
                OnPropertyChanged(nameof(HasFormStatus));
        }
    }

    public string? ImportFilePath
    {
        get => _importFilePath;
        set => SetProperty(ref _importFilePath, value);
    }

    public string? ImportStatus
    {
        get => _importStatus;
        set
        {
            if (SetProperty(ref _importStatus, value))
                OnPropertyChanged(nameof(HasImportStatus));
        }
    }

    public string? RawTextBlock
    {
        get => _rawTextBlock;
        set => SetProperty(ref _rawTextBlock, value);
    }

    public string? TextBlockStatus
    {
        get => _textBlockStatus;
        set
        {
            if (SetProperty(ref _textBlockStatus, value))
                OnPropertyChanged(nameof(HasTextBlockStatus));
        }
    }

    public string? ServiceStatus
    {
        get => _serviceStatus;
        set
        {
            if (SetProperty(ref _serviceStatus, value))
                OnPropertyChanged(nameof(HasServiceStatus));
        }
    }

    public ICommand OpenCreateCommand { get; }
    public ICommand EditSelectedCommand => _editSelectedCommand;
    public ICommand DeleteSelectedCommand => _deleteSelectedCommand;
    public ICommand ToggleFilterCommand { get; }
    public ICommand ToggleImportCommand { get; }
    public ICommand ToggleTextBlockCommand { get; }
    public ICommand ApplyFilterCommand { get; }
    public ICommand ResetFilterCommand { get; }
    public ICommand SaveObservationCommand { get; }
    public ICommand BrowseImportFileCommand { get; }
    public ICommand RunImportCommand { get; }
    public ICommand ParseTextBlockCommand { get; }
    public ICommand PrevPageCommand => _prevPageCommand;
    public ICommand NextPageCommand => _nextPageCommand;
    public ICommand CloseFormCommand { get; }
    public ICommand CloseFilterCommand { get; }
    public ICommand CloseImportCommand { get; }
    public ICommand CloseTextBlockCommand { get; }
    public ICommand AddParticipantCommand { get; }
    public ICommand ApplyParticipantSuggestionCommand => _applyParticipantSuggestionCommand;

    private async Task InitializeAsync()
    {
        if (!CanUseDataServices)
        {
            ServiceStatus = "Не налаштовано доступ до даних. Додайте змінну середовища INTERCEPTION_DESKTOP_CONNECTION_STRING або ConnectionStrings__DefaultConnection.";
            return;
        }

        try
        {
            var actions = await _actionService.GetAllAsync();
            ReplaceCollection(_actions, actions);
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Помилка ініціалізації: {ex.Message}";
        }
    }

    private async Task LoadPageAsync()
    {
        if (_queryService is null)
            return;

        Loading = true;
        try
        {
            var filter = new InterceptionFilterDto
            {
                DateFrom = FilterDateFrom,
                DateTo = FilterDateTo,
                Frequency = Normalize(FilterFrequency),
                VectorSignal = Normalize(FilterVectorSignal),
                ParticipantName = Normalize(FilterParticipant),
                LabelName = Normalize(FilterLabel)
            };

            var result = await _queryService.GetPagedAsync(filter, CurrentPage, PageSize);
            TotalCount = result.TotalCount;
            CurrentPage = Math.Clamp(CurrentPage, 1, Math.Max(1, result.TotalPages));

            ReplaceCollection(_pagedItems, result.Items.Select(MapRecord));
            _prevPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Помилка завантаження: {ex.Message}";
        }
        finally
        {
            Loading = false;
        }
    }

    private void OpenCreate()
    {
        _editingId = null;
        ResetForm();
        ToggleModal(ModalMode.Form);
        OnPropertyChanged(nameof(FormMode));
    }

    private async Task OpenEditSelectedAsync()
    {
        if (_queryService is null || SelectedObservation is null)
            return;

        try
        {
            var message = await _queryService.GetByIdAsync(SelectedObservation.Id);
            if (message is null)
            {
                FormStatus = "Перехоплення не знайдено.";
                return;
            }

            _editingId = message.Id;
            var displayDate = ConverterDateTimeExtensions.ToDisplay(message.ObservedDate);
            FormObservedDate = displayDate.Date;
            FormObservedTime = displayDate.ToString("HH:mm");
            FormFrequency = message.Frequency;
            FormDivision = message.Division;
            FormPointSignal = message.PointSignal;
            FormVectorSignal = message.VectorSignal;
            FormLabels = string.Join(", ", message.Labels.OrderBy(x => x.NameLabel).Select(x => x.NameLabel));
            FormNote = message.Note;
            SelectedAction = _actions.FirstOrDefault(x => x.Id == message.InterceptionActionId);
            FormStatus = null;

            _participantItems.Clear();
            foreach (var participant in message.Participants.OrderBy(x => x.Ordinal))
            {
                _participantItems.Add(new ParticipantDraft(this)
                {
                    Ordinal = participant.Ordinal,
                    Name = participant.Name,
                    Role = participant.Role,
                    IsUnknown = participant.IsUnknown
                });
            }

            EnsureAtLeastOneParticipant();
            ToggleModal(ModalMode.Form);
            OnPropertyChanged(nameof(FormMode));
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Помилка завантаження форми: {ex.Message}";
        }
    }

    private async Task SaveObservationAsync()
    {
        if (_commandService is null)
        {
            FormStatus = "Сервіс збереження недоступний.";
            return;
        }

        FormStatus = null;

        if (SelectedAction is null || SelectedAction.Id == Guid.Empty)
        {
            FormStatus = "Оберіть дію з довідника.";
            return;
        }

        if (!TimeSpan.TryParse(FormObservedTime, out var timeOfDay))
        {
            FormStatus = "Некоректний час. Очікується формат HH:mm.";
            return;
        }

        var observedLocal = FormObservedDate.Date.Add(timeOfDay);
        var participants = BuildParticipants();

        if (participants.Count == 0)
        {
            FormStatus = "Додайте хоча б одного учасника.";
            return;
        }

        var form = new InterceptionFormDto
        {
            ObservedDate = observedLocal,
            Frequency = Normalize(FormFrequency),
            Division = Normalize(FormDivision),
            PointSignal = Normalize(FormPointSignal),
            VectorSignal = Normalize(FormVectorSignal),
            InterceptionActionId = SelectedAction.Id,
            Note = Normalize(FormNote),
            Participants = participants,
            Labels = SplitCsv(FormLabels)
        };

        try
        {
            if (_editingId.HasValue)
                await _commandService.UpdateAsync(_editingId.Value, form);
            else
                await _commandService.CreateAsync(form, "desktop-operator");

            IsFormOpen = false;
            _editingId = null;
            await LoadPageAsync();
            ResetForm();
            OnPropertyChanged(nameof(FormMode));
        }
        catch (Exception ex)
        {
            FormStatus = ex.Message;
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_commandService is null || SelectedObservation is null)
            return;

        try
        {
            await _commandService.DeleteAsync(SelectedObservation.Id);
            SelectedObservation = null;
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Помилка видалення: {ex.Message}";
        }
    }

    private async Task ApplyFilterAsync()
    {
        CurrentPage = 1;
        await LoadPageAsync();
        IsFilterOpen = false;
    }

    private async Task ResetFilterAsync()
    {
        FilterDateFrom = null;
        FilterDateTo = null;
        FilterFrequency = null;
        FilterVectorSignal = null;
        FilterParticipant = null;
        FilterLabel = null;
        CurrentPage = 1;
        await LoadPageAsync();
        IsFilterOpen = false;
    }

    private void BrowseImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Оберіть файл імпорту",
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            ImportFilePath = dialog.FileName;
            ImportStatus = null;
        }
    }

    private async Task RunImportAsync()
    {
        if (_importService is null)
        {
            ImportStatus = "Сервіс імпорту недоступний.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ImportFilePath) || !File.Exists(ImportFilePath))
        {
            ImportStatus = "Оберіть валідний файл .xlsx.";
            return;
        }

        try
        {
            await using var stream = File.OpenRead(ImportFilePath);
            var result = await _importService.ImportAsync(stream, "desktop-operator");

            ImportStatus = $"Імпортовано: {result.ImportedCount}. Пропущено: {result.SkippedCount}.";
            if (result.Errors.Count > 0)
            {
                var details = string.Join(Environment.NewLine, result.Errors.Take(5).Select(x => $"Рядок {x.RowNumber}: {x.Message}"));
                ImportStatus += Environment.NewLine + details;
            }

            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            ImportStatus = ex.Message;
        }
    }

    private async Task ParseTextBlockAsync()
    {
        if (string.IsNullOrWhiteSpace(RawTextBlock))
        {
            TextBlockStatus = "Вставте текст для розбору.";
            return;
        }

        var result = TextBlockParser.Parse(RawTextBlock);
        if (!result.IsSuccess)
        {
            TextBlockStatus = result.Error ?? "Не вдалося розібрати текстовий блок.";
            return;
        }

        ResetForm();

        var observed = result.ObservedDate.HasValue
            ? ConverterDateTimeExtensions.ToDisplay(ConverterDateTimeExtensions.ToUtc(result.ObservedDate.Value))
            : DateTime.Now;

        FormObservedDate = observed.Date;
        FormObservedTime = observed.ToString("HH:mm");
        FormFrequency = result.Frequency;
        FormDivision = result.Division;
        FormVectorSignal = result.VectorSignal;
        FormNote = result.Note;

        _participantItems.Clear();
        var ordinal = 1;
        _participantItems.Add(new ParticipantDraft(this)
        {
            Ordinal = ordinal++,
            Name = result.Initiator,
            IsUnknown = result.Initiator is null
        });

        foreach (var responder in result.Responders)
        {
            _participantItems.Add(new ParticipantDraft(this)
            {
                Ordinal = ordinal++,
                Name = responder,
                IsUnknown = responder is null
            });
        }

        EnsureAtLeastOneParticipant();
        await PopulateParticipantRolesAsync();

        TextBlockStatus = "Блок розібрано. Оберіть дію та перевірте форму перед збереженням.";
        IsTextBlockOpen = false;
        IsFormOpen = true;
        OnPropertyChanged(nameof(FormMode));
    }

    private async Task GoToPageAsync(int page)
    {
        CurrentPage = Math.Clamp(page, 1, TotalPages);
        await LoadPageAsync();
    }

    private void ToggleModal(ModalMode mode)
    {
        IsFormOpen = mode == ModalMode.Form && !IsFormOpen;
        IsFilterOpen = mode == ModalMode.Filter && !IsFilterOpen;
        IsImportOpen = mode == ModalMode.Import && !IsImportOpen;
        IsTextBlockOpen = mode == ModalMode.Text && !IsTextBlockOpen;

        if (mode == ModalMode.Form && IsFormOpen)
            FormStatus = null;
        if (mode == ModalMode.Import && IsImportOpen)
            ImportStatus = null;
        if (mode == ModalMode.Text && IsTextBlockOpen)
            TextBlockStatus = null;
    }

    private void ResetForm()
    {
        _editingId = null;
        var now = DateTime.Now;
        FormObservedDate = now.Date;
        FormObservedTime = now.ToString("HH:mm");
        FormFrequency = null;
        FormDivision = null;
        FormPointSignal = null;
        FormVectorSignal = null;
        FormLabels = null;
        FormNote = null;
        SelectedAction = null;
        FormStatus = null;
        FrequencySuggestions.Clear();
        VectorSuggestions.Clear();
        ParticipantSuggestions.Clear();
        SelectedParticipant = null;
        SelectedParticipantSuggestion = null;
        _participantItems.Clear();
        EnsureAtLeastOneParticipant();
    }

    private void AddParticipant()
    {
        var ordinal = _participantItems.Count == 0 ? 1 : _participantItems.Max(x => x.Ordinal) + 1;
        _participantItems.Add(new ParticipantDraft(this)
        {
            Ordinal = ordinal,
            IsUnknown = true
        });
    }

    private void RemoveParticipant(ParticipantDraft draft)
    {
        if (_participantItems.Count <= 1)
            return;

        _participantItems.Remove(draft);
        ReindexParticipants();
        if (SelectedParticipant == draft)
        {
            SelectedParticipant = null;
            ParticipantSuggestions.Clear();
        }
    }

    internal async Task OnParticipantNameChangedAsync(ParticipantDraft draft)
    {
        if (_suggestionService is null)
            return;

        if (draft.IsUnknown || string.IsNullOrWhiteSpace(draft.Name))
        {
            if (SelectedParticipant == draft)
                ParticipantSuggestions.Clear();
            return;
        }

        var suggestions = await _suggestionService.GetParticipantSuggestionsAsync(draft.Name.Trim(), 8);
        if (SelectedParticipant == draft)
        {
            ReplaceCollection(_participantSuggestions, suggestions);
        }

        var exact = suggestions.FirstOrDefault(x => string.Equals(x.Name, draft.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact is not null && !string.IsNullOrWhiteSpace(exact.Role))
            draft.Role = exact.Role;
    }

    private async Task LoadParticipantSuggestionsAsync(ParticipantDraft? draft)
    {
        if (_suggestionService is null || draft is null || draft.IsUnknown || string.IsNullOrWhiteSpace(draft.Name))
        {
            ParticipantSuggestions.Clear();
            return;
        }

        var suggestions = await _suggestionService.GetParticipantSuggestionsAsync(draft.Name.Trim(), 8);
        ReplaceCollection(_participantSuggestions, suggestions);
    }

    private void ApplySelectedParticipantSuggestion()
    {
        if (SelectedParticipant is null || SelectedParticipantSuggestion is null)
            return;

        SelectedParticipant.IsUnknown = false;
        SelectedParticipant.Name = SelectedParticipantSuggestion.Name;
        SelectedParticipant.Role = SelectedParticipantSuggestion.Role;
        ParticipantSuggestions.Clear();
        SelectedParticipantSuggestion = null;
    }

    private void ApplyFrequencySuggestion(FrequencySuggestionDto suggestion)
    {
        FormFrequency = suggestion.Frequency;

        if (!string.IsNullOrWhiteSpace(suggestion.Division))
            FormDivision = suggestion.Division;

        if (!string.IsNullOrWhiteSpace(suggestion.VectorSignal))
            FormVectorSignal = suggestion.VectorSignal;

        FrequencySuggestions.Clear();
        ClearSelectedFrequencySuggestion();
    }

    private void ApplyVectorSuggestion(string suggestion)
    {
        FormVectorSignal = suggestion;
        VectorSuggestions.Clear();
        ClearSelectedVectorSuggestion();
    }

    private void ClearSelectedFrequencySuggestion()
    {
        if (_selectedFrequencySuggestion is null)
            return;

        _selectedFrequencySuggestion = null;
        OnPropertyChanged(nameof(SelectedFrequencySuggestion));
    }

    private void ClearSelectedVectorSuggestion()
    {
        if (_selectedVectorSuggestion is null)
            return;

        _selectedVectorSuggestion = null;
        OnPropertyChanged(nameof(SelectedVectorSuggestion));
    }

    private async Task PopulateParticipantRolesAsync()
    {
        foreach (var participant in _participantItems.Where(x => !x.IsUnknown && !string.IsNullOrWhiteSpace(x.Name)))
            await OnParticipantNameChangedAsync(participant);
    }

    private async Task RefreshFrequencySuggestionsAsync(string? query)
    {
        if (_suggestionService is null)
            return;

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            FrequencySuggestions.Clear();
            return;
        }

        var suggestions = await _suggestionService.GetFrequencyWithDivisionAsync(query.Trim(), 8);
        ReplaceCollection(_frequencySuggestions, suggestions);
    }

    private async Task RefreshVectorSuggestionsAsync(string? query)
    {
        if (_suggestionService is null)
            return;

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            VectorSuggestions.Clear();
            return;
        }

        var suggestions = await _suggestionService.GetVectorSignalSuggestionsAsync(query.Trim(), Normalize(FormFrequency), 8);
        ReplaceCollection(_vectorSuggestions, suggestions);
    }

    private List<ParticipantFormDto> BuildParticipants()
    {
        return [.. _participantItems
            .OrderBy(x => x.Ordinal)
            .Where(x => x.IsUnknown || !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Role))
            .Select(x => new ParticipantFormDto
            {
                Ordinal = x.Ordinal,
                Name = x.IsUnknown ? null : Normalize(x.Name),
                Role = Normalize(x.Role),
                IsUnknown = x.IsUnknown || string.IsNullOrWhiteSpace(x.Name)
            })];
    }

    private void EnsureAtLeastOneParticipant()
    {
        if (_participantItems.Count > 0)
            return;

        _participantItems.Add(new ParticipantDraft(this) { Ordinal = 1, IsUnknown = false });
        _participantItems.Add(new ParticipantDraft(this) { Ordinal = 2, IsUnknown = true });
    }

    private void ReindexParticipants()
    {
        var ordinal = 1;
        foreach (var participant in _participantItems.OrderBy(x => x.Ordinal))
            participant.Ordinal = ordinal++;
    }

    private static ObservationRecord MapRecord(InterceptionListItemDto item)
        => new()
        {
            Id = item.Id,
            ObservedDate = ConverterDateTimeExtensions.ToDisplay(item.ObservedDate),
            Frequency = item.Frequency,
            Division = item.Division,
            VectorSignal = item.VectorSignal,
            Participants = [.. item.Participants.Select(x => x.DisplayName)],
            ActionName = item.ActionName,
            Labels = [.. item.Labels]
        };

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> SplitCsv(string? value)
        => value?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
           ?? [];

    public sealed class ObservationRecord
    {
        public Guid Id { get; init; }
        public DateTime ObservedDate { get; init; }
        public string? Frequency { get; init; }
        public string? Division { get; init; }
        public string? VectorSignal { get; init; }
        public IReadOnlyList<string> Participants { get; init; } = [];
        public string? ActionName { get; init; }
        public IReadOnlyList<string> Labels { get; init; } = [];

        public string ParticipantsDisplay => Participants.Count == 0 ? "—" : string.Join(", ", Participants);
        public string LabelsDisplay => Labels.Count == 0 ? "—" : string.Join(", ", Labels);
    }

    public sealed class ParticipantDraft : ViewModelBase
    {
        private readonly ObservationsViewModel _owner;
        private int _ordinal;
        private string? _name;
        private string? _role;
        private bool _isUnknown;

        public ParticipantDraft(ObservationsViewModel owner)
        {
            _owner = owner;
            RemoveCommand = new RelayCommand(() => _owner.RemoveParticipant(this));
        }

        public int Ordinal
        {
            get => _ordinal;
            set => SetProperty(ref _ordinal, value);
        }

        public string? Name
        {
            get => _name;
            set
            {
                if (!SetProperty(ref _name, value))
                    return;

                _owner.SelectedParticipant = this;
                _ = _owner.OnParticipantNameChangedAsync(this);
            }
        }

        public string? Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        public bool IsUnknown
        {
            get => _isUnknown;
            set
            {
                if (!SetProperty(ref _isUnknown, value))
                    return;

                OnPropertyChanged(nameof(CanEditName));
                _owner.SelectedParticipant = this;
                if (value)
                {
                    Name = null;
                    Role = null;
                }
            }
        }

        public bool CanEditName => !IsUnknown;

        public ICommand RemoveCommand { get; }
    }

    private enum ModalMode
    {
        Form,
        Filter,
        Import,
        Text
    }
}
