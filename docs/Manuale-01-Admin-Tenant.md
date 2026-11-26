# HR ProTools Manuale amministratore tenant

Edizione del 23 novembre 2026. Destinatario: **Administrador Tenant**, ruolo applicativo `TenantAdministrator`.

L’amministratore tenant configura e gestisce il proprio gruppo aziendale: organizzazione, dipendenti, accessi, orari, ferie aggiuntive, circuito delle richieste e logo. Le istruzioni sono in italiano; le etichette riportate in **grassetto** corrispondono all’interfaccia in spagnolo. I percorsi indicati si aggiungono all’indirizzo abituale dell’applicazione.

## 1 Accesso e ambito di lavoro

1. Apri l’invito ricevuto dall’amministratore globale, entro 24 ore.
2. Inserisci e conferma una password di almeno 12 caratteri, con maiuscola, minuscola, numero e simbolo.
3. Premi **Activar acceso**, poi accedi da **Iniciar sesión** con email e password.
4. Usa il menu laterale; su smartphone aprilo con il controllo di navigazione.
5. Per terminare la sessione scegli **Cerrar sesion**. Per recuperare la password usa **¿Olvidó su contraseña?** nella pagina di accesso.

Il ruolo riguarda il gruppo assegnato alla tua utenza. La gestione del catalogo legale e delle festività ufficiali appartiene all’amministratore globale. Per approvare richieste servono anche il ruolo operativo appropriato e l’assegnazione al relativo livello del circuito; il solo ruolo amministrativo non basta.

## 2 Mappa delle funzioni

| Menu | Percorso | Operazione principale |
|---|---|---|
| Funcionarios | `/tenant/funcionarios` | Anagrafiche, rapporti, variazioni e accessi |
| Organizacion | `/tenant/organizacion` | Imprese, dipartimenti e mansioni |
| Jornadas y feriados | `/tenant/jornadas` | Orari e calendario integrato |
| Políticas de vacaciones | `/tenant/vacaciones/politicas` | Giorni di ferie aggiuntivi |
| Circuito de solicitudes | `/tenant/vacaciones/circuito` | Livelli, autorizzatori, ambito HR e notifiche |
| Solicitudes | `/tenant/solicitudes` | Stato amministrativo dei circuiti |
| Importación Excel | `/tenant/importacion` | Importazione iniziale da modello Excel |
| Catálogo legal | `/tenant/permisos-legales` | Consultazione delle categorie vigenti |
| Identidad visual | `/tenant/identidad-visual` | Logo del gruppo |

Se possiedi anche il ruolo `Employee`, le tue richieste personali si presentano dalle voci **Autoservicio**, secondo il [manuale utente](Manuale-05-Utente.md).

## 3 Ordine di configurazione iniziale

1. Controlla l’impresa iniziale in **Organizacion**.
2. Crea i dipartimenti e i cargos necessari.
3. Crea gli orari in **Jornadas y feriados** e controlla il calendario dell’anno.
4. Configura gli eventuali giorni di ferie aggiuntivi per impresa.
5. Registra o importa i dipendenti, assegnando un orario.
6. Attiva gli accessi e assegna i ruoli operativi.
7. Configura autorizzatori per dipartimento e ambito di HR.
8. Controlla il circuito prima di chiedere ai dipendenti di presentare richieste.

Un ruolo HR senza ambito o un autorizzatore senza dipartimento assegnato non completano la configurazione operativa.

## 4 Imprese dipartimenti e cargos

Apri **Organizacion** e seleziona la scheda interessata.

### Imprese

Compila **Nueva empresa**, inserendo ragione sociale, RUC e gli altri dati richiesti, quindi salva. Il codice viene assegnato dal sistema e compare nell’elenco. Usa **Editar** per aggiornare i dati dell’impresa.

### Dipartimenti

Seleziona **Departamentos**, scegli l’impresa e inserisci il nome. Salva e verifica il nuovo elemento nell’elenco. Il codice è automatico. **Desactivar** impedisce l’uso del dipartimento nelle nuove selezioni senza eliminare lo storico; **Activar** lo rende nuovamente disponibile.

### Cargos

Seleziona **Cargos**, scegli l’impresa e inserisci il nome della mansione. **Nivel** è facoltativo. Il codice è automatico. Puoi modificare il nome o attivare/disattivare il cargo dall’elenco.

Correggere il nome di un dipartimento o di un cargo aggiorna il dato organizzativo. Per trasferire un dipendente devi invece registrare un **Cambio de departamento o cargo** sul suo rapporto: questa operazione crea una nuova assegnazione nello storico.

## 5 Orari e festività

### Creare un orario

1. Apri **Jornadas y feriados**.
2. In **Nueva jornada laboral** indica nome, inizio, fine ed eventuale pausa.
3. Seleziona i giorni abituali di lavoro.
4. Premi **Guardar jornada**.
5. Assegna l’orario durante l’alta del dipendente oppure con **Editar → Cambio de jornada** su un rapporto esistente.

Quando cambi un orario assegnato, indica **Vigente desde**: il sistema conserva la precedente assegnazione.

### Consultare il calendario integrato

In **Calendario integrado** seleziona anno, impresa e origine. Il calendario e la lista mostrano festività **Oficial** e giorni **Adicional de empresa**. Una data coincidente si conta una sola volta.

Le date ufficiali sono in sola lettura. Per correggerle, comunica all’amministratore globale anno, nome della festività, data interessata e riferimento documentale. Controlla l’avviso di calendario **incompleto / pendiente de confirmación**: un anno non caricato non equivale a un anno senza festività.

### Aggiungere o togliere un giorno aziendale

1. Premi **Agregar feriado** nella sezione **Feriados adicionales**.
2. Seleziona impresa, data e descrizione.
3. Premi **Guardar adicional**.
4. Verifica la voce nella vista dell’anno e dell’impresa corretti.
5. Per rimuovere un giorno errato, premi **Quitar** sulla voce aziendale, indica il motivo e scegli **Confirmar baja lógica**.

L’aggiunta o la rimozione modifica i nuovi calcoli. Le richieste già presentate conservano il calendario applicato alla presentazione. Se compare un avviso su richieste future con calendario diverso, confronta i casi con HR: il sistema non li ricalcola automaticamente.

## 6 Registrare un dipendente

1. Apri **Funcionarios** e premi **Nuevo funcionario**. Il modulo è chiuso all’apertura della pagina.
2. Inserisci **Cédula**, **Nombres**, **Apellidos** e **Sexo**: scegli esplicitamente M o F.
3. Indica **Fecha de ingreso** e seleziona impresa, dipartimento e cargo.
4. Compila email, cellulare e salario lordo.
5. Seleziona **Jornada laboral** per consentire il calcolo delle richieste.
6. Se desideri invitare subito la persona, seleziona **Enviar invitación de acceso** e verifica l’email.
7. Premi **Registrar funcionario** e controlla il messaggio di esito e la riga creata.

Il **legajo**, cioè la matricola del rapporto lavorativo, viene assegnato automaticamente senza duplicare i numeri esistenti. Nell’importazione iniziale si conserva invece la matricola riportata nel file.

Se impresa, dipartimento o cargo non compaiono, verifica che siano attivi e che dipartimento e cargo appartengano all’impresa selezionata.

## 7 Modificare dati e rapporto lavorativo

Nell’elenco **Funcionarios** premi **Editar** sulla persona interessata.

| Sezione | Procedura | Risultato |
|---|---|---|
| Datos basicos | Aggiorna i campi e premi **Guardar datos** | Aggiorna i dati anagrafici e di contatto |
| Cambio de departamento o cargo | Seleziona destinazione e decorrenza; premi **Registrar cambio** | Conserva la precedente assegnazione e apre la nuova |
| Nuevo salario | Indica importo e decorrenza; premi **Registrar salario** | Aggiunge una nuova validità salariale |
| Cambio de jornada | Seleziona orario e decorrenza; premi **Registrar jornada** | Conserva la precedente assegnazione dell’orario |
| Cerrar relacion laboral | Indica data e motivo; premi **Cerrar vinculo** | Chiude il rapporto conservandone i dati storici |

Se la decorrenza è respinta, controlla quella della validità corrente e il messaggio visualizzato. La chiusura del rapporto e la disattivazione dell’account sono operazioni distinte: verifica anche lo stato dell’accesso nella ficha.

### Completare Sexo su dati storici

Apri **Ficha → Datos generales**. Se il dato è **Pendiente de completar**, usa l’editor **Actualizar sexo registrado**, seleziona M o F, indica **Motivo de la actualización** e premi **Guardar sexo**. Non dedurre il valore dal nome. La modifica conserva il proprio storico e non ricalcola licenze già presentate.

## 8 Consultare la ficha e gli storici

Da **Funcionarios** premi **Ficha**.

- **Datos generales**: persona, matricola e rapporto consultato.
- **Historial laboral**: impresa, dipartimento, cargo, date, motivo e stato. La validità corrente è evidenziata; possono comparire precedenti rapporti della stessa persona con i rispettivi legajos.
- **Historial salarial**: salario lordo, valuta e intervalli di validità, dal più recente.
- **Vacaciones**: periodi, maturazione, disponibilità, riserve e consumi.
- **Acceso al sistema**: account, inviti, attivazione e ruoli.

Una riga **Finalizado** appartiene allo storico: non va eliminata per far apparire solo la situazione corrente.

## 9 Accessi e ruoli

Apri **Ficha → Acceso al sistema**.

### Invito e attivazione

Controlla l’indirizzo email, poi premi **Enviar invitación** o **Reenviar invitación**. L’interessato imposta personalmente la password tramite il collegamento ricevuto. Se l’applicazione mostra un **Enlace manual**, consegnalo soltanto al destinatario previsto: permette di completare l’attivazione.

**Desactivar acceso** impedisce l’accesso senza eliminare il rapporto. **Reactivar acceso** è utilizzabile su un account già attivato; non sostituisce l’accettazione dell’invito iniziale. **Asociar cuenta existente** collega un account compatibile già presente: verifica attentamente l’email prima di confermare.

### Ruoli

Nel riquadro **Roles**, `Employee` è mantenuto per l’autoservizio. Puoi aggiungere `Approver`, `HumanResources` o `TenantAdministrator`, poi premere **Guardar roles**.

Dopo aver assegnato `Approver`, configura i dipartimenti autorizzati. Dopo `HumanResources`, configura l’ambito aziendale. Fai uscire e rientrare l’utente per verificare il nuovo menu dopo una variazione dei ruoli. L’assegnazione di ruoli non consente di approvare le proprie richieste.

## 10 Configurare il circuito delle richieste

Apri **Circuito de solicitudes**.

1. In **Niveles del circuito** controlla ordine, nome e tipo dei livelli. Il circuito ordinario inizia con **Jefe / Autorizador** e termina con **Recursos Humanos**. Premi **Guardar niveles**.
2. In **Autorizadores por departamento** scegli il dipartimento, seleziona gli utenti e premi **Guardar autorizadores**.
3. In **Alcance de Recursos Humanos** scegli l’utente HR e assegna **Todo el grupo empresarial** oppure le imprese interessate; premi **Guardar alcance HR**.
4. In **Cancelación por el funcionario** decidi se permettere l’annullamento di richieste pendenti o approvate e salva.

Le nuove configurazioni valgono per nuove richieste. Quelle già presentate conservano il circuito assegnato. Per i diritti legali protetti si usano i livelli HR come verifica dei requisiti, senza approvazione discrezionale del capo.

La sezione **Solicitudes anteriores**, quando presente, riguarda vecchie richieste pendenti prive di circuito. Usa **Aplicar el circuito actual a las pendientes** solo dopo aver verificato con HR le richieste da regolarizzare; non è un comando generale per riscrivere tutti i circuiti.

In **Solicitudes** puoi seguire lo stato amministrativo. Se compare **Datos restringidos** o mancano i pulsanti decisionali, il solo ruolo amministrativo non dà accesso operativo a quel contenuto.

## 11 Ferie aggiuntive e periodi

Apri **Políticas de vacaciones**, scegli l’impresa e la decorrenza. Inserisci soltanto i giorni **adicionales** per le tre fasce di anzianità e premi **Guardar nueva versión**.

La schermata mantiene fisse le basi applicative 12, 18 e 30 e mostra il risultato come base più aggiuntivi. Non inserire il totale nella casella degli aggiuntivi: per aggiungere due giorni alla base, inserisci **2**.

**Generar períodos pendientes de causación** genera i periodi maturati mancanti. Controlla il risultato nella **Ficha → Vacaciones**; non usare una nuova politica per correggere a posteriori un saldo storico già riconosciuto.

## 12 Importazione Excel

1. Apri **Importación Excel** e premi **Descargar plantilla**.
2. Mantieni i fogli **Funcionarios** e **Vacaciones**, con le intestazioni del modello.
3. In **Funcionarios** riporta i legajos esistenti nell’archivio di provenienza, i dati personali, impresa, dipartimento, cargo, orario e Sexo M/F per le persone nuove. Usa codici o nomi non ambigui già registrati nell’applicazione; per l’orario usa il nome esatto.
4. In **Vacaciones** indica, per ogni legajo importato, periodi, data di acquisizione, giorni riconosciuti e giorni già utilizzati. Mantieni il foglio anche se non hai righe da importare.
5. Salva in `.xlsx`, entro 10 MB, e selezionalo in **Seleccionar y validar**.
6. Controlla conteggi ed errori per foglio e riga. La validazione non conferma l’importazione.
7. Correggi tutti gli errori, ricarica il file e premi **Confirmar importación** solo dopo la verifica del riepilogo.
8. Controlla anagrafiche e saldi nelle fichas create.

Le matricole duplicate vengono respinte. L’importazione serve a nuovi rapporti e relativi periodi iniziali, non a caricare saldi su una matricola già presente. Non è una procedura per aggiornare in massa i dipendenti esistenti; controlla con attenzione i dati personali quando importi un nuovo rapporto di una persona già registrata.

## 13 Logo del gruppo

Apri **Identidad visual**, scegli **Seleccionar imagen**, controlla l’anteprima e premi **Guardar cambios**. Sono ammessi PNG, JPEG e WebP fino a 2 MB. Per sostituire il logo seleziona un altro file e salva; **Eliminar logo** ripristina l’intestazione senza logo aziendale. Il logo appare nelle schermate del gruppo e non modifica l’identità dell’amministrazione globale.

## 14 Notifiche e problemi comuni

In **Circuito de solicitudes → Notificaciones por correo** consulta pendenti, fallite e inviate. Dopo la correzione del problema di posta, **Reintentar fallidas** prepara nuovi tentativi. La configurazione SMTP compete all’amministratore globale. Un errore di email non annulla una richiesta o una decisione già salvata.

| Problema | Cosa controllare |
|---|---|
| Nessun autorizzatore disponibile | Accesso attivo, ruolo Approver e dipartimento assegnato |
| HR non riceve una nuova richiesta | Ruolo, ambito aziendale, livello attualmente pendente e circuito della richiesta |
| Nessun giorno lavorabile nel calcolo | Orario assegnato, decorrenza, giorni settimanali e calendario |
| Invito scaduto | Reinvialo dalla ficha; non ricreare il dipendente |
| File respinto dall’antivirus | Usa un file valido; segnala indisponibilità dello scanner al referente tecnico |
| Giorni di ferie inattesi | Confronta periodi, giorni aggiuntivi, riserve e consumi nella ficha |
| Festività ufficiale errata | Richiedi la correzione all’amministratore globale |

Per segnalare un problema riporta funzione, legajo, date, operazione e messaggio visualizzato. Non inviare password o documenti medici nelle segnalazioni generiche.
