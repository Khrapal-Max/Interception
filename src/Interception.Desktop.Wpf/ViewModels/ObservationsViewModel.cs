using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Interception.Desktop.Wpf.Infrastructure;

namespace Interception.Desktop.Wpf.ViewModels;

/// <summary>
/// Модель представлення реєстру спостережень з локальними сценаріями:
/// фільтрація, пагінація, створення/редагування, імпорт і додавання з текстового блоку.
/// </summary>
public sealed class ObservationsViewModel : ViewModelBase
{
    private const int PageSize = 10;

    private readonly ObservableCollection<ObservationRecord> _allItems = [];
    private readonly ObservableCollection<ObservationRecord> _pagedItems = [];

    private readonly RelayCommand _editSelectedCommand;
    private readonly RelayCommand _deleteSelectedCommand;
    private readonly RelayCommand _prevPageCommand;
    private readonly RelayCommand _nextPageCommand;

    private ObservationRecord? _selectedObservation;
    private int _currentPage = 1;

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
    private string? _formFrequency;
    private string? _formDivision;
    private string? _formVectorSignal;
    private string? _formParticipants;
    private string? _formActionName;
    private string? _formLabels;
    private string? _formNote;

    private string? _importText;
    private string? _importStatus;

    private string? _rawTextBlock;
    private string? _textBlockStatus;

    public ObservationsViewModel()
    {
        SeedRecords();

        OpenCreateCommand = new RelayCommand(OpenCreate);
        _editSelectedCommand = new RelayCommand(OpenEditSelected, () => SelectedObservation is not null);
        _deleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedObservation is not null);

        ToggleFilterCommand = new RelayCommand(() => ToggleDrawer(DrawerMode.Filter));
        ToggleImportCommand = new RelayCommand(() => ToggleDrawer(DrawerMode.Import));
        ToggleTextBlockCommand = new RelayCommand(() => ToggleDrawer(DrawerMode.Text));

        ApplyFilterCommand = new RelayCommand(ApplyFilter);
        ResetFilterCommand = new RelayCommand(ResetFilter);
        SaveObservationCommand = new RelayCommand(SaveObservation);
        RunImportCommand = new RelayCommand(RunImport);
        ParseTextBlockCommand = new RelayCommand(ParseTextBlock);

        _prevPageCommand = new RelayCommand(() => GoToPage(CurrentPage - 1), () => CurrentPage > 1);
        _nextPageCommand = new RelayCommand(() => GoToPage(CurrentPage + 1), () => CurrentPage < TotalPages);

        RefreshPage();
    }

    public string Title => "Реєстр спостережень";

    public ObservableCollection<ObservationRecord> PagedItems => _pagedItems;

    public ObservationRecord? SelectedObservation
    {
        get => _selectedObservation;
        set
        {
            if (!SetProperty(ref _selectedObservation, value))
            {
                return;
            }

            _editSelectedCommand.RaiseCanExecuteChanged();
            _deleteSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageSummary));
            }
        }
    }

    public int TotalCount => GetFiltered().Count;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public string PageSummary => $"Сторінка {CurrentPage} з {TotalPages}. Записів: {TotalCount}";

    public bool HasActiveFilter =>
        FilterDateFrom.HasValue ||
        FilterDateTo.HasValue ||
        !string.IsNullOrWhiteSpace(FilterFrequency) ||
        !string.IsNullOrWhiteSpace(FilterVectorSignal) ||
        !string.IsNullOrWhiteSpace(FilterParticipant) ||
        !string.IsNullOrWhiteSpace(FilterLabel);

    public DateTime? FilterDateFrom
    {
        get => _filterDateFrom;
        set
        {
            if (SetProperty(ref _filterDateFrom, value))
            {
                OnPropertyChanged(nameof(HasActiveFilter));
            }
        }
    }

    public DateTime? FilterDateTo
    {
        get => _filterDateTo;
        set
        {
            if (SetProperty(ref _filterDateTo, value))
            {
                OnPropertyChanged(nameof(HasActiveFilter));
            }
        }
    }

    public string? FilterFrequency
    {
        get => _filterFrequency;
        set
        {
            if (SetProperty(ref _filterFrequency, value))
            {
                OnPropertyChanged(nameof(HasActiveFilter));
            }
        }
    }

    public string? FilterVectorSignal
    {
        get => _filterVectorSignal;
        set
        {
            if (SetProperty(ref _filterVectorSignal, value))
            {
                OnPropertyChanged(nameof(HasActiveFilter));
            }
        }
    }

    public string? FilterParticipant
    {
        get => _filterParticipant;
        set
        {
            if (SetProperty(ref _filterParticipant, value))
            {
                OnPropertyChanged(nameof(HasActiveFilter));
            }
        }
    }

    public string? FilterLabel
    {
        get => _filterLabel;
        set
        {
            if (SetProperty(ref _filterLabel, value))
            {
                OnPropertyChanged(nameof(HasActiveFilter));
            }
        }
    }

    public bool IsFormOpen
    {
        get => _isFormOpen;
        set => SetProperty(ref _isFormOpen, value);
    }

    public bool IsFilterOpen
    {
        get => _isFilterOpen;
        set => SetProperty(ref _isFilterOpen, value);
    }

    public bool IsImportOpen
    {
        get => _isImportOpen;
        set => SetProperty(ref _isImportOpen, value);
    }

    public bool IsTextBlockOpen
    {
        get => _isTextBlockOpen;
        set => SetProperty(ref _isTextBlockOpen, value);
    }

    public string FormMode => _editingId.HasValue ? "Редагування" : "Нове повідомлення";

    public DateTime FormObservedDate
    {
        get => _formObservedDate;
        set => SetProperty(ref _formObservedDate, value);
    }

    public string? FormFrequency
    {
        get => _formFrequency;
        set => SetProperty(ref _formFrequency, value);
    }

    public string? FormDivision
    {
        get => _formDivision;
        set => SetProperty(ref _formDivision, value);
    }

    public string? FormVectorSignal
    {
        get => _formVectorSignal;
        set => SetProperty(ref _formVectorSignal, value);
    }

    public string? FormParticipants
    {
        get => _formParticipants;
        set => SetProperty(ref _formParticipants, value);
    }

    public string? FormActionName
    {
        get => _formActionName;
        set => SetProperty(ref _formActionName, value);
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

    public string? ImportText
    {
        get => _importText;
        set => SetProperty(ref _importText, value);
    }

    public string? ImportStatus
    {
        get => _importStatus;
        set => SetProperty(ref _importStatus, value);
    }

    public string? RawTextBlock
    {
        get => _rawTextBlock;
        set => SetProperty(ref _rawTextBlock, value);
    }

    public string? TextBlockStatus
    {
        get => _textBlockStatus;
        set => SetProperty(ref _textBlockStatus, value);
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
    public ICommand RunImportCommand { get; }
    public ICommand ParseTextBlockCommand { get; }
    public ICommand PrevPageCommand => _prevPageCommand;
    public ICommand NextPageCommand => _nextPageCommand;

    private void OpenCreate()
    {
        _editingId = null;
        FormObservedDate = DateTime.Now;
        FormFrequency = string.Empty;
        FormDivision = string.Empty;
        FormVectorSignal = string.Empty;
        FormParticipants = string.Empty;
        FormActionName = string.Empty;
        FormLabels = string.Empty;
        FormNote = string.Empty;

        ToggleDrawer(DrawerMode.Form);
        OnPropertyChanged(nameof(FormMode));
    }

    private void OpenEditSelected()
    {
        if (SelectedObservation is null)
        {
            return;
        }

        _editingId = SelectedObservation.Id;
        FormObservedDate = SelectedObservation.ObservedDate;
        FormFrequency = SelectedObservation.Frequency;
        FormDivision = SelectedObservation.Division;
        FormVectorSignal = SelectedObservation.VectorSignal;
        FormParticipants = string.Join(", ", SelectedObservation.Participants);
        FormActionName = SelectedObservation.ActionName;
        FormLabels = string.Join(", ", SelectedObservation.Labels);
        FormNote = SelectedObservation.Note;

        ToggleDrawer(DrawerMode.Form);
        OnPropertyChanged(nameof(FormMode));
    }

    private void SaveObservation()
    {
        var participants = SplitCsv(FormParticipants);
        var labels = SplitCsv(FormLabels);

        if (_editingId.HasValue)
        {
            var existing = _allItems.FirstOrDefault(x => x.Id == _editingId.Value);
            if (existing is not null)
            {
                existing.ObservedDate = FormObservedDate;
                existing.Frequency = FormFrequency;
                existing.Division = FormDivision;
                existing.VectorSignal = FormVectorSignal;
                existing.Participants = participants;
                existing.ActionName = FormActionName;
                existing.Labels = labels;
                existing.Note = FormNote;
            }
        }
        else
        {
            _allItems.Insert(0, new ObservationRecord
            {
                Id = Guid.NewGuid(),
                ObservedDate = FormObservedDate,
                Frequency = FormFrequency,
                Division = FormDivision,
                VectorSignal = FormVectorSignal,
                Participants = participants,
                ActionName = FormActionName,
                Labels = labels,
                Note = FormNote
            });
        }

        IsFormOpen = false;
        _editingId = null;
        RefreshPage();
        OnPropertyChanged(nameof(FormMode));
    }

    private void DeleteSelected()
    {
        if (SelectedObservation is null)
        {
            return;
        }

        _allItems.Remove(SelectedObservation);
        SelectedObservation = null;
        RefreshPage();
    }

    private void ApplyFilter()
    {
        CurrentPage = 1;
        RefreshPage();
    }

    private void ResetFilter()
    {
        FilterDateFrom = null;
        FilterDateTo = null;
        FilterFrequency = string.Empty;
        FilterVectorSignal = string.Empty;
        FilterParticipant = string.Empty;
        FilterLabel = string.Empty;
        CurrentPage = 1;
        RefreshPage();
    }

    private void RunImport()
    {
        if (string.IsNullOrWhiteSpace(ImportText))
        {
            ImportStatus = "Немає даних для імпорту.";
            return;
        }

        var imported = 0;
        var skipped = 0;

        var rows = ImportText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var row in rows)
        {
            var cells = row.Split(';');
            if (cells.Length < 7 || !DateTime.TryParse(cells[0], out var observedDate))
            {
                skipped++;
                continue;
            }

            _allItems.Insert(0, new ObservationRecord
            {
                Id = Guid.NewGuid(),
                ObservedDate = observedDate,
                Frequency = cells.ElementAtOrDefault(1),
                Division = cells.ElementAtOrDefault(2),
                VectorSignal = cells.ElementAtOrDefault(3),
                Participants = SplitCsv(cells.ElementAtOrDefault(4)),
                ActionName = cells.ElementAtOrDefault(5),
                Labels = SplitCsv(cells.ElementAtOrDefault(6)),
                Note = cells.ElementAtOrDefault(7)
            });

            imported++;
        }

        ImportStatus = $"Імпортовано: {imported}, пропущено: {skipped}.";
        RefreshPage();
    }

    private void ParseTextBlock()
    {
        if (string.IsNullOrWhiteSpace(RawTextBlock))
        {
            TextBlockStatus = "Вставте текст для розбору.";
            return;
        }

        var rows = RawTextBlock.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (rows.Length < 5)
        {
            TextBlockStatus = "Недостатньо рядків. Мінімум: дата/час, частота, Р/М, ініціатор, відповідач.";
            return;
        }

        if (!DateTime.TryParse(rows[0], CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var observedDate) &&
            !DateTime.TryParse(rows[0], out observedDate))
        {
            TextBlockStatus = "Не вдалося розпізнати дату і час у першому рядку.";
            return;
        }

        _editingId = null;
        FormObservedDate = observedDate;
        FormFrequency = rows[1];
        FormDivision = rows[2];
        FormVectorSignal = rows[2];
        FormParticipants = string.Join(", ", rows.Skip(3).Take(2));
        FormActionName = string.Empty;
        FormLabels = string.Empty;
        FormNote = string.Join(Environment.NewLine, rows.Skip(5));

        TextBlockStatus = "Текст розібрано. Перевірте поля та натисніть «Зберегти» у формі.";
        ToggleDrawer(DrawerMode.Form);
        OnPropertyChanged(nameof(FormMode));
    }

    private void GoToPage(int page)
    {
        CurrentPage = Math.Clamp(page, 1, TotalPages);
        RefreshPage();
    }

    private void RefreshPage()
    {
        var filtered = GetFiltered();

        if (CurrentPage > Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize)))
        {
            CurrentPage = 1;
        }

        _pagedItems.Clear();
        foreach (var item in filtered
                     .OrderByDescending(x => x.ObservedDate)
                     .Skip((CurrentPage - 1) * PageSize)
                     .Take(PageSize))
        {
            _pagedItems.Add(item);
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageSummary));
        _prevPageCommand.RaiseCanExecuteChanged();
        _nextPageCommand.RaiseCanExecuteChanged();
    }

    private List<ObservationRecord> GetFiltered()
    {
        var query = _allItems.AsEnumerable();

        if (FilterDateFrom.HasValue)
        {
            query = query.Where(x => x.ObservedDate >= FilterDateFrom.Value);
        }

        if (FilterDateTo.HasValue)
        {
            query = query.Where(x => x.ObservedDate <= FilterDateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(FilterFrequency))
        {
            query = query.Where(x => ContainsIgnoreCase(x.Frequency, FilterFrequency));
        }

        if (!string.IsNullOrWhiteSpace(FilterVectorSignal))
        {
            query = query.Where(x => ContainsIgnoreCase(x.VectorSignal, FilterVectorSignal));
        }

        if (!string.IsNullOrWhiteSpace(FilterParticipant))
        {
            query = query.Where(x => x.Participants.Any(p => ContainsIgnoreCase(p, FilterParticipant)));
        }

        if (!string.IsNullOrWhiteSpace(FilterLabel))
        {
            query = query.Where(x => x.Labels.Any(l => ContainsIgnoreCase(l, FilterLabel)));
        }

        return query.ToList();
    }

    private static bool ContainsIgnoreCase(string? source, string? query)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        return source.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> SplitCsv(string? value)
    {
        return value?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList()
            ?? [];
    }

    private void ToggleDrawer(DrawerMode mode)
    {
        IsFormOpen = mode == DrawerMode.Form && !IsFormOpen;
        IsFilterOpen = mode == DrawerMode.Filter && !IsFilterOpen;
        IsImportOpen = mode == DrawerMode.Import && !IsImportOpen;
        IsTextBlockOpen = mode == DrawerMode.Text && !IsTextBlockOpen;
    }

    private void SeedRecords()
    {
        _allItems.Add(new ObservationRecord
        {
            Id = Guid.NewGuid(),
            ObservedDate = DateTime.Now.AddHours(-3),
            Frequency = "410.1370",
            Division = "69 обрп",
            VectorSignal = "Маліївка - Січневе",
            Participants = ["ЗВЕЗДА", "ЦИГАН"],
            ActionName = "Діалог",
            Labels = ["укх", "голос"],
            Note = "Тестовий запис для демонстрації."
        });

        _allItems.Add(new ObservationRecord
        {
            Id = Guid.NewGuid(),
            ObservedDate = DateTime.Now.AddHours(-1),
            Frequency = "303.500",
            Division = "р/м 3",
            VectorSignal = "Сектор північ",
            Participants = ["ПАНДА", "НВ"],
            ActionName = "Доповідь",
            Labels = ["терміново"],
            Note = "Коротка доповідь про рух техніки."
        });
    }

    /// <summary>
    /// Локальна модель рядка таблиці спостережень.
    /// </summary>
    public sealed class ObservationRecord
    {
        public Guid Id { get; set; }
        public DateTime ObservedDate { get; set; }
        public string? Frequency { get; set; }
        public string? Division { get; set; }
        public string? VectorSignal { get; set; }
        public List<string> Participants { get; set; } = [];
        public string? ActionName { get; set; }
        public List<string> Labels { get; set; } = [];
        public string? Note { get; set; }

        public string ParticipantsDisplay => Participants.Count == 0 ? "—" : string.Join(", ", Participants);
        public string LabelsDisplay => Labels.Count == 0 ? "—" : string.Join(", ", Labels);
    }

    private enum DrawerMode
    {
        Form,
        Filter,
        Import,
        Text
    }

}
