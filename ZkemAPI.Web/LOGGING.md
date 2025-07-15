# Konfiguracja logowania

## Lokalizacja plików logów

Po uruchomieniu aplikacji logi będą zapisywane w folderze `logs/` w katalogu aplikacji:

- `nlog-all-YYYY-MM-DD.log` - wszystkie logi (włączając system)
- `nlog-own-YYYY-MM-DD.log` - tylko logi aplikacji ZkemAPI
- `nlog-errors-YYYY-MM-DD.log` - tylko błędy
- `nlog-lifecycle-YYYY-MM-DD.log` - wydarzenia startu/stopu aplikacji  
- `internal-nlog.txt` - logi wewnętrzne NLog (do debugowania konfiguracji)

## Co jest logowane

### Aplikacja główna:
- Start/stop aplikacji
- Wszystkie nieobsłużone wyjątki
- Połączenia z czytnikami ZKTeco
- Operacje API (pobieranie danych, zapisywanie itp.)
- Błędy komunikacji

### Poziomy logowania:
- **DEBUG**: Szczegółowe informacje o wykonywanych operacjach
- **INFO**: Ogólne informacje o działaniu aplikacji
- **WARN**: Ostrzeżenia (np. nieudane połączenia)
- **ERROR**: Błędy z pełnymi stacktrace'ami

## Diagnozowanie problemów

1. **Aplikacja się zamyka** - sprawdź:
   - `nlog-lifecycle-YYYY-MM-DD.log` - wydarzenia startu/stopu
   - `nlog-errors-YYYY-MM-DD.log` - krytyczne błędy
   - `internal-nlog.txt` - problemy z konfiguracją logowania

2. **Problemy z czytnikami** - sprawdź:
   - `nlog-own-YYYY-MM-DD.log` - szczegóły komunikacji z urządzeniami

3. **Błędy API** - sprawdź:
   - `nlog-errors-YYYY-MM-DD.log` - pełne stacktrace błędów

## Konfiguracja

Poziom logowania można zmienić w `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",     // Poziom dla wszystkich komponentów
      "ZkemAPI": "Debug"      // Poziom dla aplikacji ZkemAPI
    }
  }
}
```

Dostępne poziomy: Trace, Debug, Information, Warning, Error, Critical 