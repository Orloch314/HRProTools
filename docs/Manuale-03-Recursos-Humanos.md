# HR ProTools Manuale Recursos Humanos

Edizione del 23 novembre 2026. Destinatario: **Recursos Humanos**, ruolo `HumanResources`.

HR gestisce le richieste al proprio livello di competenza, verifica i requisiti dei permessi legali, risolve le assenze e consulta alert e report. Le istruzioni sono in italiano; menu e pulsanti sono riportati in spagnolo. I percorsi si aggiungono all’indirizzo abituale dell’applicazione.

## 1 Preparare l’accesso

Completa l’invito ricevuto entro 24 ore, scegliendo una password di almeno 12 caratteri con maiuscola, minuscola, numero e simbolo. Premi **Activar acceso**, poi accedi da **Iniciar sesión**. Per recuperare la password usa **¿Olvidó su contraseña?**; al termine scegli **Cerrar sesion**.

L’amministratore tenant deve assegnarti sia il ruolo HR sia un **Alcance de Recursos Humanos**, riferito a determinate imprese o a tutto il gruppo. Il ruolo da solo non sostituisce questa configurazione. Le richieste sono inoltre visibili e risolvibili secondo gli attori e i livelli conservati nel loro circuito.

| Menu | Percorso | Uso |
|---|---|---|
| Dashboard | `/dashboard` | Contatori operativi e alert HR |
| Solicitudes | `/tenant/solicitudes` | Ferie e permessi |
| Ausencias | `/tenant/ausencias` | Registrazione e risoluzione delle assenze |
| Reporte de permisos | `/tenant/reportes/permisos` | Esportazione Excel dei permessi |
| Preaviso verificado | `/tenant/permisos/preaviso` | Presupposto per la ricerca di lavoro durante preavviso |

Se hai anche `Employee`, presenti le tue richieste dall’**Autoservicio**, come descritto nel [manuale utente](Manuale-05-Utente.md). Non puoi approvare o respingere una tua richiesta.

## 2 Controllo quotidiano

1. Apri **Dashboard** e controlla **Solicitudes pendientes** e **Ausencias por revisar**.
2. Esamina gli **Alertas operativas**, in particolare documentazione e periodi di ferie pendenti.
3. Apri **Solicitudes** e lavora prima sugli elementi che riportano **Resolver**.
4. Controlla **Ausencias** per dichiarazioni ancora da classificare.
5. Dopo aver gestito il problema all’origine di un alert, usa **Marcar atendida**.

Segnare un alert come gestito non approva una richiesta, non giustifica un’assenza e non modifica un saldo: esegui prima l’operazione appropriata.

## 3 Approvare o respingere ferie

1. Apri **Solicitudes → Vacaciones**.
2. Nella tabella **Vacaciones pendientes de mi aprobación** premi **Resolver**.
3. Verifica persona, impresa, dipartimento, intervallo, giorni riservati e storico del circuito.
4. Inserisci il commento; per il rigetto devi indicare il motivo.
5. Premi **Aprobar nivel** oppure **Rechazar solicitud** e controlla il messaggio e il nuovo stato.

L’approvazione di HR chiude la richiesta soltanto se è l’ultimo livello previsto. Altrimenti la richiesta passa al livello successivo. Una riga nelle tabelle di stato non implica che tu possa ancora decidere: il pulsante dipende dal livello pendente e dall’assegnazione al circuito.

La presentazione riserva il saldo; l’approvazione finale conferma il consumo. Rigetto e annullamento seguono le operazioni del sistema senza richiedere rettifiche manuali del saldo. Le date vengono conteggiate secondo l’orario e il calendario conservati nella richiesta.

## 4 Gestire permessi ordinari

Apri **Solicitudes → Permisos**, scegli **Resolver** e controlla tipo, goce, date, quantità, motivo e circuito. Per i permessi discrezionali usa **Aprobar nivel** o **Rechazar solicitud**, motivando il rigetto.

I permessi non consumano il saldo ferie. Le quantità possono essere espresse in giorni o in ore/minuti; nei registri ricorrenti controlla l’intero intervallo. Il tipo e la regola mostrati appartengono alla versione conservata alla presentazione, anche se il catalogo globale è stato modificato successivamente.

## 5 Verificare un diritto legale protetto

La verifica HR accerta i presupposti della categoria; non è l’approvazione discrezionale del capo. Le richieste protette usano i livelli HR del circuito.

1. Apri il dettaglio del permesso in **Solicitudes → Permisos**.
2. Leggi categoria, versione, durata, regime e documentazione richiesta.
3. Controlla i dati specifici dell’evento e i documenti disponibili.
4. Nel riquadro **Verificación de requisitos legales por HR** scegli un risultato.
5. Inserisci **Fundamento y excepciones documentadas**, da 20 a 1000 caratteri, indicando elementi concreti pertinenti alla verifica.
6. Se la documentazione può essere presentata successivamente nel caso applicabile, registra un **Plazo documental posterior** valido; non usare una data scaduta.
7. Premi **Registrar verificación HR**.
8. Se il caso è definibile, completa poi la decisione con i pulsanti del circuito.

| Risultato | Significato operativo |
|---|---|
| Requiere información (`PENDING`) | La verifica resta aperta; occorrono elementi ulteriori |
| Requisitos verificados (`VERIFIED`) | Presupposti verificati; permette l’approvazione del diritto protetto |
| Requisitos legales no cumplidos (`NOT_MET`) | Presupposti non soddisfatti, con motivazione; permette il rigetto |

**Registrar verificación HR** salva la verifica, ma non conclude da solo il circuito. L’applicazione blocca l’approvazione di un diritto non verificato e il rigetto discrezionale di un diritto verificato. Per confermare i requisiti occorre documentazione con scansione pulita oppure il termine documentale successivo espressamente registrato, quando applicabile.

Le categorie e i limiti ufficiali sono mantenuti dall’amministratore globale. Se una regola appare errata, segnala codice, versione e situazione: non sostituirla con un permesso ordinario per aggirarne i controlli.

## 6 Documenti dei permessi

Il dipendente può caricare documenti nella propria richiesta presentata; HR può caricarli e scaricare quelli accessibili nel proprio ambito dal dettaglio del permesso.

Sono ammessi **PDF, PNG e JPEG fino a 5 MB**. Dopo la scelta del file il pannello esegue il caricamento e mostra lo stato antivirus. Usa **Descargar** solo quando disponibile: il download HR è abilitato per lo stato **Clean**. Un file non scansionato o bloccato non equivale a un documento verificato.

Registra nei commenti solo il fondamento necessario alla decisione; conserva la documentazione medica nel riquadro dedicato, evitando diagnosi nei commenti del circuito o nei messaggi generici.

## 7 Registrare un’assenza

1. Apri **Ausencias**.
2. In **Registrar ausencia** seleziona un dipendente dell’elenco autorizzato.
3. Indica data iniziale, data finale e **Tipo declarado**: medica, ingiustificata o altra giustificata.
4. Inserisci il motivo e premi **Registrar ausencia**.
5. Controlla il record nella tabella **Ausencias dentro de mi alcance**.

Il tipo dichiarato non conclude la classificazione. Se esiste una ferie o un permesso sovrapposto, il sistema avvisa. Solo dopo aver esaminato il caso puoi selezionare **Confirmar registro si existe permiso o vacacion superpuesta** e registrare l’assenza: la conferma non annulla la richiesta preesistente. Due assenze attive sovrapposte restano vietate.

## 8 Risolvere o correggere un’assenza

1. Premi **Ver detalle** sul record.
2. In **Resolucion RR. HH.** scegli **Clasificacion final**.
3. Seleziona **Estado final**: **Justificada** oppure **Injustificada**.
4. Inserisci il commento che sostiene la risoluzione.
5. Premi **Guardar resolucion** e controlla lo storico aggiornato.

Per correggere un record che non deve restare attivo, indica il motivo nel commento e premi **Cancelar registro**. La cancellazione è logica e conserva lo storico; non elimina il record. Se serve un nuovo record corretto, verifica prima che quello errato sia effettivamente annullato.

### Giustificativi delle assenze

Nel dettaglio seleziona il file e premi **Cargar y escanear**. Sono ammessi PDF, JPEG e PNG fino a 5 MB. Verifica lo stato antivirus e usa **Descargar** soltanto quando abilitato.

Per le assenze, il dipendente dichiara il fatto ma i documenti giustificativi vengono caricati da HR. Questo flusso è diverso da quello dei documenti dei permessi, che il dipendente può allegare direttamente alla propria richiesta.

## 9 Registrare il preavviso verificato

Apri **Preaviso verificado**.

1. Seleziona il dipendente nel tuo ambito.
2. Indica il periodo notificato, da **Desde** a **Hasta**.
3. Seleziona la modalità esclusiva proposta: **Dos horas diarias** oppure **Un día por semana**.
4. Inserisci la **Referencia documental del preaviso notificado**.
5. Premi **Registrar preaviso verificado**.

Questa operazione registra il presupposto per la richiesta di ricerca di lavoro durante preavviso. Non presenta una richiesta per conto del dipendente: sarà l’interessato a utilizzare **Mis permisos**, entro il periodo e con la modalità verificati.

## 10 Esportare il report dei permessi

Apri **Reporte de permisos**. Seleziona impresa, dipartimento, date ed eventuale stato, poi premi **Descargar Excel**.

Le opzioni **Todas las autorizadas** e **Todos los autorizados** si riferiscono al tuo ambito. Il filtro iniziale include richieste con data iniziale dal giorno indicato; il filtro finale limita la data iniziale al giorno indicato. Non è un filtro generale di tutte le richieste che si sovrappongono all’intervallo.

Il file contiene dati operativi dei permessi, quantità, goce, stato e risoluzione, senza salari o allegati medici. Controlla filtri e destinatario prima di distribuirlo.

## 11 Collaborazione con l’amministratore tenant

Nomina, variazioni di dipartimento/cargo/salario, orari, ruoli e calendario aziendale vengono gestiti dalle funzioni amministrative tenant. Il menu **Funcionarios** è riservato al ruolo tenant administrator; un collegamento del dashboard non assegna automaticamente quel ruolo.

Se devi consultare una ficha individuale già individuata e autorizzata, il percorso è `/tenant/funcionarios/{id del rapporto}`: richiedi il collegamento all’amministratore, senza inventare l’identificativo. La ficha espone gli storici secondo le autorizzazioni applicative; le operazioni di modifica degli accessi e di completamento di Sexo restano amministrative. Per le procedure della nomina, se possiedi anche il ruolo tenant administrator, usa il [relativo manuale](Manuale-01-Admin-Tenant.md).

Una modifica del calendario ufficiale o aziendale non ricalcola le richieste già presentate. Se l’amministratore segnala richieste future con un calendario differente, confronta richiesta, date e decisioni e concorda la gestione del singolo caso; non rettificare in massa i saldi.

## 12 Problemi frequenti

| Situazione | Controllo o intervento |
|---|---|
| Elenco HR vuoto | Chiedi verifica del ruolo e dell’ambito aziendale |
| Richiesta visibile senza decisione | Controlla il livello pendente, gli attori del circuito e se è una tua richiesta |
| Approvazione legale bloccata | Registra la verifica e controlla documenti o termine successivo |
| Rigetto legale bloccato | Verifica il risultato della revisione; un diritto verificato non è rigettabile discrezionalmente |
| Download bloccato | Controlla stato antivirus e ambito autorizzato |
| Giorni conteggiati inattesi | Confronta orario, riposi e festività della richiesta; chiedi verifica all’amministratore |
| Email di avviso non arrivata | Controlla la richiesta nell’applicazione; chiedi all’admin tenant il controllo delle notifiche |

Per una segnalazione indica legajo, funzione, date, stato e messaggio visualizzato, condividendo soltanto i dati necessari.
