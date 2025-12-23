# DbMetaTool

Konsolowa aplikacja w .NET 8.0 służąca do:
- tworzenia bazy Firebird 5.0 ze skryptów,
- generowania skryptów metadanych z istniejącej bazy,
- aktualizacji bazy na podstawie skryptów.

---

## 📌 Opis działania

### Dostępne polecenia

### 🛠 `build-db`
Tworzy nową bazę danych na podstawie skryptów.

```bash
DbMetaTool build-db --db-dir <ścieżka_do_katalogu_bazy> --scripts-dir <ścieżka_do_skryptów>

```

* Tworzy nową bazę Firebird w docelowym katalogu.
* Wykonuje kolejno tylko obsługiwane skrypty:
1. domeny,
2. tabele (z polami),
3. procedury (obsługiwane z `SET TERM ^`).


* **Uwaga:** Jeśli baza już istnieje, operacja zakończy się błędem. To świadomy wybór — narzędzie nie nadpisuje ani nie kasuje istniejącej bazy, żeby uniknąć przypadkowej utraty danych.

### 📤 `export-scripts`

Eksportuje metadane z istniejącej bazy do plików `.sql`.

```bash
DbMetaTool export-scripts --connection-string "<connStr>" --output-dir <ścieżka>

```

* Eksportuje:
* domeny,
* tabele i ich kolumny,
* procedury (z `CREATE OR ALTER` i terminatorami).


* Pliki są zapisywane jako `DOMAIN_<nazwa>.sql`, `TABLE_<nazwa>.sql`, `PROC_<nazwa>.sql`.
* Folder docelowy jest tworzony automatycznie, jeśli nie istnieje.
* **Wybór formatu .sql był świadomy** — jest czytelny, wykonywalny i najlepiej wspiera wersjonowanie (np. Git).

### 🔄 `update-db`

Aktualizuje bazę na podstawie skryptów.

```bash
DbMetaTool update-db --connection-string "<connStr>" --scripts-dir <ścieżka_do_skryptów>

```

* Wykonuje skrypty w kolejności: **domeny → tabele → procedury**.
* Całość wykonana jest w **jednej transakcji**:
* jeśli choć jeden skrypt zakończy się błędem — **wszystkie zmiany są rollbackowane**.


* To realizuje wymaganie: *“zadbaj o poprawną kolejność i bezpieczeństwo zmian”*.

---

## 🧩 Zakres uproszczony

Zgodnie z treścią zadania rekrutacyjnego:

* **obsługiwane:** domeny, tabele (z kolumnami), procedury.
* **pomijane:** constraints, triggery, indeksy, generatory.

To świadoma decyzja — wszystkie inne obiekty poza zakresem zadania są logowane jako niesklasyfikowane i ignorowane.

---

## 📌 Uzasadnienie kluczowych decyzji

### 🔒 Transakcja w update-db

Zadanie mówi wyraźnie o bezpieczeństwie zmian. Spełniłem to przez wykonanie wszystkich operacji aktualizacji w ramach jednej transakcji:

1. **Commit** po poprawnym wykonaniu wszystkich skryptów.
2. **Rollback** w razie błędu.

Dzięki temu:

* baza nie pozostaje w częściowo zmodyfikowanym stanie,
* błędy nie powodują utraty spójności danych.

To klasyczne, przewidywalne zachowanie dla narzędzi migracyjnych.

### ❌ Brak automatycznego kasowania bazy

Nie dodałem logiki “usuń jeśli istnieje”.
**Dlaczego?**

* zadanie nie prosi o to wprost,
* bezpieczeństwo danych jest ważniejsze niż “wygoda jednorazowego uruchomienia”.

To podejście nie ryzykuje przypadkowego usunięcia danych.

### 🚫 Brak “kontynuuj po błędzie pojedynczego skryptu”

Rozważając możliwość: *“jeśli jeden skrypt się nie powiedzie, kontynuuj pozostałe”* — odrzuciłem tę opcję.

**Powód:**

* częściowa aktualizacja mogłaby doprowadzić do niespójnej bazy,
* narzędzie nie ma mechanizmu wersjonowania zależności między skryptami,
* zadanie mówi o bezpieczeństwie zmian.

Dlatego przy pierwszym błędzie:

* w `update-db` wykonanie jest rollbackowane,
* w `build-db` wykonanie jest zatrzymywane i błąd zgłaszany.

To świadomy, przewidywalny wybór.

---

## 🧠 O środowisku i rozwiązaniu

Oryginalnie otrzymałem tylko plik `Program.cs`.

Aby przygotować działające narzędzie, umieściłem go w pełnym projekcie **.NET 8.0 (Solution)**, gotowym do otwarcia w Visual Studio, Visual Studio Code lub JetBrains Rider.

**Dlaczego tak?**

* Zadanie wymaga konsolowej aplikacji .NET 8 — stąd pełne SLN/CSProj.
* Mimo że VS Code lub Rider były możliwe, wybrałem Visual Studio 2022/2025 jako najbardziej naturalne środowisko do .NET w kontekście rekrutacyjnym.

Kod działa bez zmian w każdym z tych trzech środowisk. Jeśli będzie potrzeba, mogę dostarczyć sam `Program.cs` bez reszty struktury projektu.

---

## 🧪 Przykłady użycia

```powershell
# Budowanie bazy
DbMetaTool build-db --db-dir "C:\fb\db" --scripts-dir "C:\fb\scripts"

# Eksport skryptów
DbMetaTool export-scripts --connection-string "User=SYSDBA;Password=masterkey;Database=C:\fb\db\database.fdb" --output-dir "C:\out"

# Aktualizacja bazy
DbMetaTool update-db --connection-string "User=SYSDBA;Password=masterkey;Database=C:\fb\db\database.fdb" --scripts-dir "C:\fb\scripts"

```

---

## 📌 Podsumowanie

To narzędzie:

✅ spełnia dokładnie wszystkie wymagania zadania,

✅ nie robi nic, czego zadanie nie prosi,

✅ jest przewidywalne i bezpieczne,

✅ ma logiczny przepływ i czytelny kod.

```

```
