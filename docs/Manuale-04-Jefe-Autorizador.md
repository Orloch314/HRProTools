# HR ProTools Manuale jefe autorizador

Edizione del 23 novembre 2026. Destinatario: **Jefe / Autorizador**, ruolo `Approver`.

Il capo autorizzatore esamina le richieste assegnate al proprio livello e registra assenze nei dipartimenti autorizzati. Questo manuale usa spiegazioni in italiano ed etichette spagnole dell’applicazione. I percorsi si aggiungono all’indirizzo abituale di HR ProTools.

## 1 Attivare e verificare l’accesso

Apri l’invito entro 24 ore, imposta e conferma una password di almeno 12 caratteri con maiuscola, minuscola, numero e simbolo e premi **Activar acceso**. Accedi con email e password da **Iniciar sesión**. Usa **¿Olvidó su contraseña?** per recuperare l’accesso e **Cerrar sesion** per uscire.

L’amministratore tenant deve assegnarti il ruolo `Approver` e i dipartimenti interessati in **Autorizadores por departamento**. Le richieste già presentate conservano gli attori del proprio circuito: una variazione delle assegnazioni non riscrive automaticamente tutte le richieste precedenti.

| Menu | Percorso | Cosa mostra |
|---|---|---|
| Dashboard | `/dashboard` | Richieste pendenti, dipendenti e assenze del tuo ambito |
| Solicitudes | `/tenant/solicitudes` | Ferie e permessi del circuito assegnato |
| Ausencias | `/tenant/ausencias` | Registrazione e consultazione delle assenze autorizzate |

Per le tue richieste usa le voci **Autoservicio**, se possiedi anche il ruolo `Employee`: vedi il [manuale utente](Manuale-05-Utente.md).

## 2 Consultare le richieste pendenti

1. Apri **Solicitudes**.
2. Seleziona la scheda **Vacaciones** oppure **Permisos**.
3. Individua la riga nella tabella **pendientes de mi aprobación**.
4. Premi **Resolver**.
5. Controlla persona, dipartimento, date, quantità e precedenti decisioni.

Il pulsante decisionale compare quando la richiesta è pendente al tuo livello e sei tra gli attori assegnati. Una riga nello storico o nelle tabelle di stato non implica una nuova azione da eseguire.

## 3 Approvare una richiesta di ferie

Dopo aver aperto il dettaglio, verifica le date e i **Días reservados**. Inserisci l’eventuale commento e premi **Aprobar nivel**.

Controlla l’esito: l’approvazione del tuo livello fa proseguire il circuito. La richiesta rimane pendente se deve essere valutata da un altro autorizzatore o da HR. Il conteggio considera l’orario e il calendario registrati alla presentazione; non si ottiene contando semplicemente tutte le date tra inizio e fine.

Non serve modificare manualmente il saldo ferie. Riserva, consumo finale e successivi eventuali storni sono gestiti dal sistema.

## 4 Respingere ferie o permessi ordinari

1. Apri la richiesta con **Resolver**.
2. Inserisci un motivo chiaro nel commento.
3. Premi **Rechazar solicitud**.
4. Verifica lo stato **Rechazada** e lo storico della decisione.

Il rigetto conclude il circuito della richiesta e libera le relative riserve secondo il tipo di richiesta. Non utilizzare un rigetto per cambiare le date: comunica al dipendente il problema e concorda la presentazione corretta. Una richiesta non deve essere approvata solo per far sparire la riga dalle pendenti.

## 5 Permessi ordinari e diritti legali

In **Permisos** controlla categoria, quantità, periodo, motivo e goce. I permessi ordinari possono passare dal tuo livello secondo il circuito configurato e non consumano il saldo ferie.

I diritti legali protetti sono sottoposti a verifica dei requisiti da parte di HR. Il ruolo di jefe non attribuisce il potere di respingerli discrezionalmente, verificare certificati medici o modificare i requisiti del catalogo. Se il caso riguarda una licenza protetta, indirizza l’interessato a HR.

Non puoi approvare o respingere una richiesta presentata da te, anche quando riguarda un dipartimento che autorizzi.

## 6 Registrare un’assenza nel dipartimento

1. Apri **Ausencias**.
2. Seleziona il dipendente in **Registrar ausencia**. L’elenco contiene soltanto i rapporti nel tuo ambito.
3. Indica **Fecha inicial** e **Fecha final**.
4. Scegli **Tipo declarado**: medica, ingiustificata o altra giustificata.
5. Inserisci **Motivo o comentario**.
6. Premi **Registrar ausencia** e controlla il record nell’elenco.

La registrazione avvia la revisione; non decide definitivamente se l’assenza è giustificata. Classificazione finale, caricamento dei giustificativi e correzione tramite annullamento competono a HR.

### Quando esiste una sovrapposizione

Se l’intervallo coincide con ferie o permessi, esamina prima il messaggio. Quando l’occorrenza deve essere registrata comunque, puoi selezionare **Confirmar registro si existe permiso o vacacion superpuesta** e ripetere il salvataggio.

Questa conferma conserva le richieste già esistenti: non le annulla e non corregge automaticamente il saldo ferie. Coinvolgi HR per la valutazione del caso. Una seconda assenza attiva sovrapposta viene respinta anche con la conferma.

## 7 Consultare le assenze e lo storico

In **Ausencias dentro de mi alcance** premi **Ver detalle**. Controlla periodo, tipo dichiarato, stato e **Historial**. Gli stati possono indicare dichiarazione, revisione pendente, risoluzione giustificata/ingiustificata o annullamento.

Se rilevi un errore, comunica a HR legajo, date e correzione richiesta. Il tuo ruolo non include il caricamento o il download dei giustificativi medici. La visibilità operativa non attribuisce accesso salariale o amministrazione degli account.

## 8 Notifiche e richieste già lavorate

L’applicazione può inviare email quando il tuo livello diventa pendente. Usa comunque **Solicitudes** come riferimento per lo stato attuale: un messaggio già ricevuto può riferirsi a una richiesta successivamente decisa o annullata.

Se la posta non arriva, verifica nell’applicazione e segnala all’admin tenant il problema di notifiche. Non chiedere al dipendente di inviare richieste duplicate. Le decisioni già salvate restano valide anche se un’email non viene consegnata.

## 9 Problemi frequenti

| Situazione | Cosa fare |
|---|---|
| Nessun dipendente nell’elenco | Chiedi all’admin tenant di controllare dipartimenti assegnati e ruolo |
| Il dipendente dice di aver presentato ma non vedo la richiesta | Verifica tipo, stato e assegnazione del circuito con l’amministratore |
| Vedo la richiesta ma non **Resolver** | Può appartenere a un altro livello, essere già conclusa o essere una tua richiesta |
| Il rigetto viene respinto | Inserisci il motivo; per un diritto legale protetto deve intervenire HR |
| L’assenza risulta duplicata | Controlla le assenze attive; chiedi a HR la correzione del record esistente |
| Occorrono date diverse | Il modulo decisionale non modifica il periodo richiesto; concorda la gestione con dipendente e HR |

Configurazione dei circuiti, calendario, cataloghi, nomina e ruoli si gestiscono dall’amministrazione tenant o globale secondo la funzione, non dalla schermata di approvazione.
