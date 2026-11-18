# HR ProTools — Regole generali per Codex

## 1. Repository Git

Repository ufficiale del progetto:

https://github.com/Orloch314/HRProTools

- Utilizzare questo repository per lo sviluppo di HR ProTools.
- Mantenere il codice sotto controllo versione Git.
- Non creare repository alternativi.
- Non eseguire push, force push o modifiche distruttive della cronologia senza autorizzazione.
- Non includere credenziali, password, token o altri segreti nei commit.

## 2. Cartelle di lavoro e riferimenti

| Percorso | Utilizzo | Permessi |
|---|---|---|
| `C:\HRProTools` | Directory principale del progetto | Lettura e scrittura |
| `C:\iMatch2` | Codice iMatch, riferimento per componenti riutilizzabili | **Sola lettura** |
| `C:\HRProTools\mockup` | Mockup e riferimenti grafici della UX | Lettura |
| `C:\HRProTools\docs` | Documentazione generata da Codex | Lettura e scrittura |

### Regola fondamentale: iMatch

La cartella `C:\iMatch2` deve essere trattata come **rigorosamente di sola lettura**.

Codex può:

- Analizzare il codice sorgente.
- Studiare l'architettura multitenant.
- Esaminare ASP.NET Core Identity.
- Analizzare il sistema di inviti con token.
- Studiare l'implementazione SMTP.
- Identificare componenti da riutilizzare.

Codex non deve:

- Modificare o eliminare file in `C:\iMatch2`.
- Eseguire migration sul database iMatch.
- Modificare configurazioni o credenziali iMatch.
- Eseguire operazioni che alterino il repository originale.
- Avviare script che possano modificare servizi o dati di iMatch.

Il codice necessario deve essere copiato o adattato esclusivamente all'interno del progetto HR ProTools.

## 3. Logo e mockup

Il logo ufficiale di HR ProTools è disponibile nella directory `C:\HRProTools`.

I mockup grafici sono disponibili in:

`C:\HRProTools\mockup`

Codex deve:

- Individuare il file del logo prima di implementare l'interfaccia.
- Utilizzare il logo esistente senza ridisegnarlo arbitrariamente.
- Consultare i mockup come riferimento visivo.
- Mantenere coerenza di colori, tipografia, navigazione e componenti.
- Adattare le interfacce ai differenti ruoli del software.
- Garantire una UX responsive per PC e smartphone.

I mockup sono riferimenti grafici; i documenti dei requisiti prevalgono quando esistono differenze funzionali.

## 4. Gestione della documentazione

**Tutti i documenti creati da Codex devono essere salvati in:**

`C:\HRProTools\docs`

Questo include:

- Analisi tecniche.
- Specifiche funzionali.
- Documentazione del database.
- Decisioni architetturali.
- Piani di sviluppo.
- Rapporti di implementazione.
- Risultati dei test.
- Documentazione di deployment.
- `ESTADO_PROYECTO.md`.

Non disperdere documenti in altre directory.

Eccezione: file tecnici richiesti dagli strumenti di sviluppo, come `README.md`, `AGENTS.md`, configurazioni e migration, devono rimanere nelle posizioni appropriate del repository.

Evitare documentazione ridondante: aggiornare quella esistente quando possibile.

## 5. Regole di sviluppo

- Rispettare il piano MVP approvato in cinque fasi.
- Privilegiare funzionalità operative e verificabili.
- Non introdurre microservizi o Docker.
- Utilizzare C#/.NET 10, Blazor e PostgreSQL 18.
- Mantenere un'architettura modulare semplice.
- Riutilizzare i componenti appropriati di iMatch.
- Non riprodurre il motore BPM/XML legacy.
- Non sviluppare anticipatamente i moduli futuri.
- Limitare audit e controlli a quanto necessario per il corretto funzionamento e la sicurezza dei dati.
- Non modificare i requisiti funzionali senza autorizzazione.

## 6. Conclusione di ogni fase

Al termine di ogni fase Codex deve:

1. Verificare che il progetto compili.
2. Eseguire i test essenziali pertinenti.
3. Elencare le funzionalità realmente operative.
4. Documentare eventuali problemi bloccanti.
5. Aggiornare `C:\HRProTools\docs\ESTADO_PROYECTO.md`.
6. Indicare come provare le funzionalità implementate.
7. Attendere l'autorizzazione prima di iniziare la fase successiva.

**Obiettivo principale: ottenere rapidamente un MVP funzionante, mantenendo una base tecnica adeguata all'evoluzione futura di HR ProTools.**

## 2.1 Database legacy BPM — CSV di riferimento

**Percorso:** `C:\HRProTools\base.csv`

Il file contiene l'inventario delle tabelle e delle colonne del database PostgreSQL utilizzato dal precedente sistema BPM per la gestione delle risorse umane.

### Istruzioni per Codex

Durante la Fase 1/5:

1. Leggere e analizzare `base.csv`.
2. Identificare le tabelle e i campi utili per HR ProTools.
3. Individuare le strutture relative a:
   - Anagrafica dei funzionari e rapporti lavorativi.
   - Imprese, dipartimenti e cargos.
   - Movimenti e storico del personale.
   - Salari e storico retributivo.
   - Ferie, periodi e saldi.
   - Permessi, assenze e giustificazioni.
4. Confrontare le strutture legacy con il modello dati provvisorio di HR ProTools.
5. Segnalare eventuali informazioni importanti mancanti nel nuovo modello.

### Restrizioni

- Il CSV è esclusivamente una **fonte di riferimento per il dominio HR**.
- Non replicare l'architettura BPM/XML.
- Non copiare automaticamente le tabelle legacy.
- Non presumere relazioni, tipi di dati o vincoli non documentati nel CSV.
- Progettare il database in funzione dell'architettura multitenant di HR ProTools.
- Considerare le esigenze dei moduli futuri senza implementarli nel MVP.

### Deliverable

Includere i risultati dell'analisi del CSV in:

`C:\HRProTools\docs\01-Analisis-Reutilizacion.md`

Le eventuali modifiche proposte al modello dati devono essere riportate in:

`C:\HRProTools\docs\02-Modelo-Datos-Provisional.md`