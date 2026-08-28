using System.Globalization;

namespace WinCopyQueue;

public sealed record LanguageOption(string Code, string DisplayName);

public static class Localization
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["SummaryNone"] = "No active transfers", ["SummaryOne"] = "1 active transfer", ["SummaryMany"] = "{0} active transfers",
        ["Pause"] = "Pause", ["Resume"] = "Resume", ["EmptyTitle"] = "The queue is empty",
        ["EmptyDescription"] = "New copy or move operations from Explorer will appear here automatically.",
        ["Next"] = "NEXT", ["Cancel"] = "Cancel", ["Remove"] = "Remove", ["Hide"] = "Hide to tray",
        ["CancelTip"] = "Cancel this session. Files already completed remain at the destination.",
        ["RemoveTip"] = "Remove this canceled, failed, or paused session from the queue.",
        ["FilePauseTip"] = "Pause or resume only this file", ["FileCancelTip"] = "Cancel only this file",
        ["FileList"] = "File list", ["HideList"] = "Hide list", ["ClearCompleted"] = "Clear completed",
        ["Verify"] = "Verify files after copying (SHA-256)", ["VerifyTip"] = "Detects transfer corruption but requires reading the copied file again.",
        ["Footer"] = "The window hides a few seconds after the queue becomes empty. Transfers continue in the tray.",
        ["Destination"] = "To", ["Speed"] = "Speed", ["Calculating"] = "calculating…", ["Paused"] = "paused", ["Verifying"] = "verifying SHA-256…",
        ["AllReady"] = "All files are ready", ["CanceledInfo"] = "Transfer canceled — completed files remain at the destination",
        ["FailedInfo"] = "Transfer failed", ["Preparing"] = "Preparing file list…", ["Waiting"] = "Waiting for its turn…",
        ["Copying"] = "Copying", ["Moving"] = "Moving", ["Files"] = "{0} files", ["FilesProgress"] = "{0}/{1} files",
        ["StatePreparing"] = "PREPARING", ["StateQueued"] = "QUEUED", ["StateRunning"] = "RUNNING", ["StatePaused"] = "PAUSED",
        ["StateCompleted"] = "DONE", ["StateFailed"] = "ERROR", ["StateCanceled"] = "CANCELED", ["StateSkipped"] = "SKIPPED",
        ["StateWaiting"] = "WAITING", ["Language"] = "Language", ["Version"] = "Version {0}",
        ["RemoveConfirmTitle"] = "Remove session?", ["RemovePausedConfirm"] = "This session is paused. Removing it will cancel all remaining files. Completed files will remain at the destination. Continue?",
        ["RemoveFinishedConfirm"] = "Remove this session from the list? Files at the destination will not be deleted.",
        ["ConflictTitle"] = "File conflict — WinCopyQueue", ["ConflictHeading"] = "A file with this name already exists",
        ["ConflictIntro"] = "Compare both files and choose what WinCopyQueue should do.", ["NewFile"] = "NEW FILE", ["ExistingFile"] = "EXISTING FILE",
        ["Size"] = "Size", ["Modified"] = "Modified", ["ApplyAll"] = "Apply this decision to all following conflicts in this session",
        ["CancelSession"] = "Cancel session", ["Skip"] = "Skip", ["Replace"] = "Replace",
        ["TrayReady"] = "WinCopyQueue — ready", ["TrayActive"] = "WinCopyQueue — active: {0}", ["ShowQueue"] = "Show queue",
        ["PauseQueue"] = "Pause queue", ["ResumeQueue"] = "Resume queue", ["RunAtStartup"] = "Run at startup",
        ["RepairExplorer"] = "Repair Explorer integration", ["Exit"] = "Exit WinCopyQueue", ["NoTransfers"] = "No active transfers",
        ["Added"] = "Added to queue", ["Completed"] = "Transfer completed", ["TransferError"] = "Transfer error",
        ["ExplorerPaste"] = "Paste with WinCopyQueue"
    };

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = English,
        ["pl"] = Merge(new Dictionary<string, string> {
            ["SummaryNone"]="Brak aktywnych transferów",["SummaryOne"]="1 aktywny transfer",["SummaryMany"]="{0} aktywne transfery",["Pause"]="Wstrzymaj",["Resume"]="Wznów",
            ["EmptyTitle"]="Kolejka jest pusta",["EmptyDescription"]="Nowe kopiowanie lub przenoszenie z Explorera pojawi się tutaj automatycznie.",["Next"]="NASTĘPNE",["Cancel"]="Anuluj",["Remove"]="Usuń",["Hide"]="Ukryj do traya",
            ["CancelTip"]="Anuluj sesję. Już ukończone pliki pozostaną w miejscu docelowym.",["RemoveTip"]="Usuń anulowaną, błędną lub wstrzymaną sesję z kolejki.",["FilePauseTip"]="Wstrzymaj lub wznów tylko ten plik",["FileCancelTip"]="Anuluj tylko ten plik",
            ["FileList"]="Lista plików",["HideList"]="Zwiń listę",["ClearCompleted"]="Wyczyść ukończone",["Verify"]="Weryfikuj pliki po skopiowaniu (SHA-256)",["VerifyTip"]="Wykrywa uszkodzenia transferu, ale wymaga ponownego odczytu skopiowanego pliku.",
            ["Footer"]="Okno zniknie kilka sekund po opróżnieniu kolejki. Transfer działa dalej po jego ukryciu.",["Destination"]="Do",["Speed"]="Prędkość",["Calculating"]="obliczanie…",["Paused"]="wstrzymana",["Verifying"]="weryfikacja SHA-256…",
            ["AllReady"]="Wszystkie pliki gotowe",["CanceledInfo"]="Transfer anulowany — ukończone pliki pozostają w miejscu docelowym",["FailedInfo"]="Transfer zakończył się błędem",["Preparing"]="Przygotowywanie listy plików…",["Waiting"]="Oczekuje na swoją kolej…",
            ["Copying"]="Kopiowanie",["Moving"]="Przenoszenie",["Files"]="{0} plików",["FilesProgress"]="{0}/{1} plików",["StatePreparing"]="PRZYGOTOWANIE",["StateQueued"]="W KOLEJCE",["StateRunning"]="W TOKU",["StatePaused"]="PAUZA",["StateCompleted"]="GOTOWE",["StateFailed"]="BŁĄD",["StateCanceled"]="ANULOWANO",["StateSkipped"]="POMINIĘTO",["StateWaiting"]="OCZEKUJE",
            ["Language"]="Język",["Version"]="Wersja {0}",["RemoveConfirmTitle"]="Usunąć sesję?",["RemovePausedConfirm"]="Ta sesja jest wstrzymana. Usunięcie anuluje wszystkie pozostałe pliki. Ukończone pliki pozostaną w miejscu docelowym. Kontynuować?",["RemoveFinishedConfirm"]="Usunąć tę sesję z listy? Pliki w miejscu docelowym nie zostaną usunięte.",
            ["ConflictTitle"]="Konflikt plików — WinCopyQueue",["ConflictHeading"]="Plik o tej nazwie już istnieje",["ConflictIntro"]="Porównaj oba pliki i zdecyduj, co ma zrobić WinCopyQueue.",["NewFile"]="NOWY PLIK",["ExistingFile"]="ISTNIEJĄCY PLIK",["Size"]="Rozmiar",["Modified"]="Zmodyfikowano",["ApplyAll"]="Zastosuj tę decyzję do wszystkich kolejnych konfliktów w tej sesji",["CancelSession"]="Anuluj sesję",["Skip"]="Pomiń",["Replace"]="Zastąp",
            ["TrayReady"]="WinCopyQueue — gotowy",["TrayActive"]="WinCopyQueue — aktywne: {0}",["ShowQueue"]="Pokaż kolejkę",["PauseQueue"]="Wstrzymaj kolejkę",["ResumeQueue"]="Wznów kolejkę",["RunAtStartup"]="Uruchamiaj z systemem",["RepairExplorer"]="Napraw integrację z Explorerem",["Exit"]="Zakończ WinCopyQueue",["NoTransfers"]="Brak aktywnych transferów",["Added"]="Dodano do kolejki",["Completed"]="Transfer zakończony",["TransferError"]="Błąd transferu",["ExplorerPaste"]="Wklej z WinCopyQueue"
        }),
        ["de"] = Merge(new Dictionary<string, string> { ["SummaryNone"]="Keine aktiven Übertragungen",["SummaryOne"]="1 aktive Übertragung",["SummaryMany"]="{0} aktive Übertragungen",["Pause"]="Pausieren",["Resume"]="Fortsetzen",["EmptyTitle"]="Die Warteschlange ist leer",["EmptyDescription"]="Neue Kopier- oder Verschiebevorgänge aus dem Explorer erscheinen hier automatisch.",["Next"]="ALS NÄCHSTES",["Cancel"]="Abbrechen",["Remove"]="Entfernen",["ClearCompleted"]="Abgeschlossene löschen",["Verify"]="Dateien nach dem Kopieren prüfen (SHA-256)",["Footer"]="Das Fenster wird nach dem Leeren der Warteschlange ausgeblendet. Übertragungen laufen weiter.",["Destination"]="Nach",["Speed"]="Geschwindigkeit",["Calculating"]="wird berechnet…",["Paused"]="pausiert",["Verifying"]="SHA-256 wird geprüft…",["AllReady"]="Alle Dateien sind fertig",["CanceledInfo"]="Übertragung abgebrochen — fertige Dateien bleiben am Ziel",["Preparing"]="Dateiliste wird vorbereitet…",["Waiting"]="Wartet…",["Copying"]="Kopieren",["Moving"]="Verschieben",["Files"]="{0} Dateien",["FileList"]="Dateiliste",["HideList"]="Liste schließen",["Language"]="Sprache",["Version"]="Version {0}",["RemoveConfirmTitle"]="Sitzung entfernen?",["RemovePausedConfirm"]="Diese Sitzung ist pausiert. Beim Entfernen werden alle verbleibenden Dateien abgebrochen. Fortfahren?",["ConflictHeading"]="Eine Datei mit diesem Namen ist bereits vorhanden",["NewFile"]="NEUE DATEI",["ExistingFile"]="VORHANDENE DATEI",["Size"]="Größe",["Modified"]="Geändert",["ApplyAll"]="Diese Entscheidung auf alle weiteren Konflikte anwenden",["CancelSession"]="Sitzung abbrechen",["Skip"]="Überspringen",["Replace"]="Ersetzen",["ShowQueue"]="Warteschlange anzeigen",["PauseQueue"]="Warteschlange pausieren",["ResumeQueue"]="Warteschlange fortsetzen",["RunAtStartup"]="Mit Windows starten",["RepairExplorer"]="Explorer-Integration reparieren",["Exit"]="WinCopyQueue beenden",["NoTransfers"]="Keine aktiven Übertragungen" }),
        ["fr"] = Merge(new Dictionary<string, string> { ["SummaryNone"]="Aucun transfert actif",["SummaryOne"]="1 transfert actif",["SummaryMany"]="{0} transferts actifs",["Pause"]="Pause",["Resume"]="Reprendre",["EmptyTitle"]="La file est vide",["EmptyDescription"]="Les nouvelles copies ou déplacements depuis l’Explorateur apparaîtront ici.",["Next"]="SUIVANTS",["Cancel"]="Annuler",["Remove"]="Supprimer",["ClearCompleted"]="Effacer les terminés",["Verify"]="Vérifier après la copie (SHA-256)",["Footer"]="La fenêtre se masque après le vidage de la file. Les transferts continuent.",["Destination"]="Vers",["Speed"]="Vitesse",["Calculating"]="calcul…",["Paused"]="en pause",["Verifying"]="vérification SHA-256…",["AllReady"]="Tous les fichiers sont prêts",["CanceledInfo"]="Transfert annulé — les fichiers terminés restent à destination",["Preparing"]="Préparation de la liste…",["Waiting"]="En attente…",["Copying"]="Copie",["Moving"]="Déplacement",["Files"]="{0} fichiers",["FileList"]="Liste des fichiers",["HideList"]="Réduire la liste",["Language"]="Langue",["Version"]="Version {0}",["RemoveConfirmTitle"]="Supprimer la session ?",["RemovePausedConfirm"]="Cette session est en pause. La supprimer annulera les fichiers restants. Continuer ?",["ConflictHeading"]="Un fichier portant ce nom existe déjà",["NewFile"]="NOUVEAU FICHIER",["ExistingFile"]="FICHIER EXISTANT",["Size"]="Taille",["Modified"]="Modifié",["ApplyAll"]="Appliquer ce choix aux conflits suivants",["CancelSession"]="Annuler la session",["Skip"]="Ignorer",["Replace"]="Remplacer",["ShowQueue"]="Afficher la file",["PauseQueue"]="Mettre la file en pause",["ResumeQueue"]="Reprendre la file",["RunAtStartup"]="Lancer au démarrage",["RepairExplorer"]="Réparer l’intégration Explorer",["Exit"]="Quitter WinCopyQueue",["NoTransfers"]="Aucun transfert actif" }),
        ["es"] = Merge(new Dictionary<string, string> { ["SummaryNone"]="No hay transferencias activas",["SummaryOne"]="1 transferencia activa",["SummaryMany"]="{0} transferencias activas",["Pause"]="Pausar",["Resume"]="Reanudar",["EmptyTitle"]="La cola está vacía",["EmptyDescription"]="Las nuevas copias o movimientos del Explorador aparecerán aquí.",["Next"]="SIGUIENTES",["Cancel"]="Cancelar",["Remove"]="Eliminar",["ClearCompleted"]="Limpiar completadas",["Verify"]="Verificar después de copiar (SHA-256)",["Footer"]="La ventana se oculta al vaciarse la cola. Las transferencias continúan.",["Destination"]="A",["Speed"]="Velocidad",["Calculating"]="calculando…",["Paused"]="pausada",["Verifying"]="verificando SHA-256…",["AllReady"]="Todos los archivos están listos",["CanceledInfo"]="Transferencia cancelada — los archivos completados permanecen en el destino",["Preparing"]="Preparando la lista…",["Waiting"]="Esperando turno…",["Copying"]="Copiando",["Moving"]="Moviendo",["Files"]="{0} archivos",["FileList"]="Lista de archivos",["HideList"]="Ocultar lista",["Language"]="Idioma",["Version"]="Versión {0}",["RemoveConfirmTitle"]="¿Eliminar sesión?",["RemovePausedConfirm"]="Esta sesión está pausada. Eliminarla cancelará los archivos restantes. ¿Continuar?",["ConflictHeading"]="Ya existe un archivo con este nombre",["NewFile"]="ARCHIVO NUEVO",["ExistingFile"]="ARCHIVO EXISTENTE",["Size"]="Tamaño",["Modified"]="Modificado",["ApplyAll"]="Aplicar esta decisión a los conflictos siguientes",["CancelSession"]="Cancelar sesión",["Skip"]="Omitir",["Replace"]="Reemplazar",["ShowQueue"]="Mostrar cola",["PauseQueue"]="Pausar cola",["ResumeQueue"]="Reanudar cola",["RunAtStartup"]="Iniciar con Windows",["RepairExplorer"]="Reparar integración con Explorer",["Exit"]="Salir de WinCopyQueue",["NoTransfers"]="No hay transferencias activas" }),
        ["pt"] = Merge(new Dictionary<string, string> { ["SummaryNone"]="Nenhuma transferência ativa",["SummaryOne"]="1 transferência ativa",["SummaryMany"]="{0} transferências ativas",["Pause"]="Pausar",["Resume"]="Retomar",["EmptyTitle"]="A fila está vazia",["EmptyDescription"]="Novas cópias ou movimentações do Explorer aparecerão aqui.",["Next"]="PRÓXIMOS",["Cancel"]="Cancelar",["Remove"]="Remover",["ClearCompleted"]="Limpar concluídas",["Verify"]="Verificar após copiar (SHA-256)",["Footer"]="A janela é ocultada após esvaziar a fila. As transferências continuam.",["Destination"]="Para",["Speed"]="Velocidade",["Calculating"]="calculando…",["Paused"]="pausada",["Verifying"]="verificando SHA-256…",["AllReady"]="Todos os arquivos estão prontos",["CanceledInfo"]="Transferência cancelada — arquivos concluídos permanecem no destino",["Preparing"]="Preparando lista…",["Waiting"]="Aguardando…",["Copying"]="Copiando",["Moving"]="Movendo",["Files"]="{0} arquivos",["FileList"]="Lista de arquivos",["HideList"]="Ocultar lista",["Language"]="Idioma",["Version"]="Versão {0}",["RemoveConfirmTitle"]="Remover sessão?",["RemovePausedConfirm"]="Esta sessão está pausada. Removê-la cancelará os arquivos restantes. Continuar?",["ConflictHeading"]="Já existe um arquivo com este nome",["NewFile"]="NOVO ARQUIVO",["ExistingFile"]="ARQUIVO EXISTENTE",["Size"]="Tamanho",["Modified"]="Modificado",["ApplyAll"]="Aplicar esta decisão aos próximos conflitos",["CancelSession"]="Cancelar sessão",["Skip"]="Ignorar",["Replace"]="Substituir",["ShowQueue"]="Mostrar fila",["PauseQueue"]="Pausar fila",["ResumeQueue"]="Retomar fila",["RunAtStartup"]="Iniciar com o Windows",["RepairExplorer"]="Reparar integração do Explorer",["Exit"]="Sair do WinCopyQueue",["NoTransfers"]="Nenhuma transferência ativa" }),
        ["zh"] = Merge(new Dictionary<string, string> { ["SummaryNone"]="没有活动传输",["SummaryOne"]="1 个活动传输",["SummaryMany"]="{0} 个活动传输",["Pause"]="暂停",["Resume"]="继续",["EmptyTitle"]="队列为空",["EmptyDescription"]="来自资源管理器的新复制或移动任务会自动显示在此处。",["Next"]="下一项",["Cancel"]="取消",["Remove"]="移除",["ClearCompleted"]="清除已完成",["Verify"]="复制后验证文件 (SHA-256)",["Footer"]="队列清空后窗口会自动隐藏，传输将在托盘中继续。",["Destination"]="到",["Speed"]="速度",["Calculating"]="计算中…",["Paused"]="已暂停",["Verifying"]="正在验证 SHA-256…",["AllReady"]="所有文件已完成",["CanceledInfo"]="传输已取消 — 已完成文件保留在目标位置",["Preparing"]="正在准备文件列表…",["Waiting"]="等待中…",["Copying"]="复制",["Moving"]="移动",["Files"]="{0} 个文件",["FileList"]="文件列表",["HideList"]="收起列表",["Language"]="语言",["Version"]="版本 {0}",["RemoveConfirmTitle"]="移除任务？",["RemovePausedConfirm"]="此任务已暂停。移除将取消所有剩余文件。是否继续？",["ConflictHeading"]="已存在同名文件",["NewFile"]="新文件",["ExistingFile"]="现有文件",["Size"]="大小",["Modified"]="修改时间",["ApplyAll"]="将此决定应用于本任务的后续冲突",["CancelSession"]="取消任务",["Skip"]="跳过",["Replace"]="替换",["ShowQueue"]="显示队列",["PauseQueue"]="暂停队列",["ResumeQueue"]="继续队列",["RunAtStartup"]="开机启动",["RepairExplorer"]="修复资源管理器集成",["Exit"]="退出 WinCopyQueue",["NoTransfers"]="没有活动传输" }),
        ["ja"] = Merge(new Dictionary<string, string> { ["SummaryNone"]="実行中の転送はありません",["SummaryOne"]="1 件の転送を実行中",["SummaryMany"]="{0} 件の転送を実行中",["Pause"]="一時停止",["Resume"]="再開",["EmptyTitle"]="キューは空です",["EmptyDescription"]="エクスプローラーからのコピーや移動がここに表示されます。",["Next"]="次のファイル",["Cancel"]="キャンセル",["Remove"]="削除",["ClearCompleted"]="完了済みを消去",["Verify"]="コピー後に検証 (SHA-256)",["Footer"]="キューが空になるとウィンドウは非表示になります。転送は続行されます。",["Destination"]="保存先",["Speed"]="速度",["Calculating"]="計算中…",["Paused"]="一時停止中",["Verifying"]="SHA-256 を検証中…",["AllReady"]="すべてのファイルが完了しました",["CanceledInfo"]="転送をキャンセルしました — 完了済みファイルは保存先に残ります",["Preparing"]="ファイル一覧を準備中…",["Waiting"]="待機中…",["Copying"]="コピー",["Moving"]="移動",["Files"]="{0} ファイル",["FileList"]="ファイル一覧",["HideList"]="一覧を閉じる",["Language"]="言語",["Version"]="バージョン {0}",["RemoveConfirmTitle"]="セッションを削除しますか？",["RemovePausedConfirm"]="このセッションは一時停止中です。削除すると残りのファイルはキャンセルされます。続行しますか？",["ConflictHeading"]="同じ名前のファイルが既に存在します",["NewFile"]="新しいファイル",["ExistingFile"]="既存のファイル",["Size"]="サイズ",["Modified"]="更新日時",["ApplyAll"]="以降の競合にもこの決定を適用",["CancelSession"]="セッションをキャンセル",["Skip"]="スキップ",["Replace"]="置換",["ShowQueue"]="キューを表示",["PauseQueue"]="キューを一時停止",["ResumeQueue"]="キューを再開",["RunAtStartup"]="Windows 起動時に実行",["RepairExplorer"]="エクスプローラー統合を修復",["Exit"]="WinCopyQueue を終了",["NoTransfers"]="実行中の転送はありません" })
    };

    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English"), new("pl", "Polski"), new("de", "Deutsch"), new("fr", "Français"),
        new("es", "Español"), new("pt", "Português"), new("zh", "简体中文"), new("ja", "日本語")
    ];

    public static event EventHandler? LanguageChanged;
    public static string CurrentLanguage { get; private set; } = "en";

    public static void Initialize(string? savedLanguage)
    {
        var requested = string.IsNullOrWhiteSpace(savedLanguage)
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : savedLanguage;
        CurrentLanguage = Translations.ContainsKey(requested) ? requested : "en";
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(CurrentLanguage);
    }

    public static void SetLanguage(string code)
    {
        if (!Translations.ContainsKey(code) || string.Equals(code, CurrentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        CurrentLanguage = code;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(code);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Text(string key) =>
        Translations[CurrentLanguage].TryGetValue(key, out var value) ? value : English[key];

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Text(key), arguments);

    private static IReadOnlyDictionary<string, string> Merge(IReadOnlyDictionary<string, string> overrides)
    {
        var merged = new Dictionary<string, string>(English);
        foreach (var item in overrides)
        {
            merged[item.Key] = item.Value;
        }
        return merged;
    }
}
