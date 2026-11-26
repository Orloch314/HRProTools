# HR ProTools Manuale utente dipendente

Edizione del 23 novembre 2026. Destinatario: **Funcionario / usuario normal**, ruolo `Employee`.

L’autoservizio permette di consultare il proprio rapporto, chiedere ferie e permessi e dichiarare assenze. Le istruzioni sono in italiano; i nomi dei pulsanti e dei menu corrispondono all’interfaccia spagnola. I percorsi indicati si aggiungono all’indirizzo dell’applicazione comunicato dall’azienda.

## 1 Primo accesso

1. Apri l’invito ricevuto dall’azienda entro 24 ore.
2. In **Complete su invitación** inserisci la nuova password e ripetila in **Confirmar contraseña**.
3. Usa almeno 12 caratteri con una maiuscola, una minuscola, un numero e un simbolo.
4. Premi **Activar acceso**.
5. Dopo **Cuenta activada**, scegli **Ir al inicio de sesión**.
6. Inserisci email e password, poi premi **Ingresar**.

Se l’invito è scaduto, chiedi all’amministratore tenant un nuovo invito; non occorre registrare nuovamente la tua anagrafica. Se hai dimenticato la password, usa **¿Olvidó su contraseña?** dalla pagina di accesso. Per cambiare una password conosciuta usa **Cambiar contraseña** da **Mis datos**.

Su smartphone apri il menu di navigazione per scegliere la funzione. Per uscire usa **Cerrar sesion**. **Recuérdame** mantiene la sessione: usalo soltanto su un dispositivo personale.

## 2 Dove trovare le funzioni

| Menu o schermata | Percorso | Uso |
|---|---|---|
| Dashboard | `/dashboard` | Riepilogo personale |
| Mis datos | `/empleado` | Rapporto attivo e disponibilità ferie |
| Mis vacaciones | `/empleado/vacaciones` | Calcolo, presentazione e annullamento ferie |
| Mis permisos | `/empleado/permisos` | Categorie di permesso e relativo storico |
| Mis ausencias | `/empleado/ausencias` | Dichiarazione e consultazione delle assenze |

Le richieste riguardano il rapporto attivo associato alla tua utenza. Non selezioni il dipendente e non presenti richieste per altre persone.

## 3 Controllare i dati personali

Apri **Mis datos**. Controlla cédula, legajo, impresa, dipartimento e cargo. Il legajo è la matricola del rapporto lavorativo, assegnata dal sistema o mantenuta dall’importazione iniziale.

La schermata mostra anche **Vacaciones disponibles** e **Días reservados**. I giorni disponibili sono quelli ancora utilizzabili; quelli riservati sono impegnati dalle richieste pendenti. Il riepilogo del dashboard può mostrare i disponibili senza decimali, mentre **Mis datos** espone il valore con due decimali.

Per correggere anagrafica, email lavorativa, dipartimento, cargo o associazione del rapporto, contatta l’amministratore tenant. Questa pagina è una consultazione, non un modulo per modificare liberamente tali dati.

## 4 Richiedere ferie

1. Apri **Mis vacaciones** oppure premi **Solicitar vacaciones** da **Mis datos**.
2. Compila **Fecha inicial** e **Fecha final**.
3. Premi **Calcular días**.
4. Nella **Confirmación del intervalo** controlla il totale e ogni data: orario applicato, riposo, festività e voce **Reserva 1 día** o **No descuenta**.
5. Se il periodo è corretto, premi **Confirmar reserva de … días**.
6. Controlla la richiesta in **Mis solicitudes**, con stato e **Nivel pendiente**.

**Calcular días** prepara il controllo; la richiesta viene presentata con la conferma della riserva. Se cambi le date, ricalcola e controlla nuovamente il periodo prima di confermare.

Il sistema esclude i riposi e le festività previste dall’orario e dal calendario della tua impresa. Una festività ufficiale coincidente con un giorno aggiuntivo aziendale si conta una sola volta. Un intervallo senza giorni lavorabili o con sovrapposizioni non ammesse viene respinto.

La richiesta presentata conserva il calendario e il conteggio applicati in quel momento. Una successiva modifica delle festività non cambia automaticamente i suoi giorni.

## 5 Capire lo stato delle ferie

| Stato | Significato |
|---|---|
| Pendiente | Presentata, in attesa del livello indicato; i giorni sono riservati |
| Aprobada | Circuito concluso con approvazione |
| Rechazada | Respinta; la riserva viene liberata |
| Cancelada | Annullata; il sistema gestisce la restituzione dei giorni interessati |

L’approvazione di un singolo livello non significa necessariamente approvazione definitiva: controlla stato e livello pendente nell’applicazione.

### Annullare ferie

In **Mis solicitudes** premi **Cancelar** sulla richiesta, se il pulsante è disponibile. L’annullamento è consentito per pendenti o approvate quando l’amministratore ha abilitato questa possibilità. Non occorre eliminare la richiesta: lo storico resta conservato.

Se il pulsante manca o devi cambiare il periodo, contatta HR per la gestione del caso. Non presentare subito una richiesta sovrapposta a quella ancora attiva.

## 6 Richiedere un permesso

1. Apri **Mis permisos** e seleziona **Tipo de permiso o licencia**.
2. Leggi durata, requisiti documentali, preavviso e regime mostrati per il tipo scelto.
3. Compila date e motivo; se è prevista la scelta dell’unità, seleziona giorni oppure ore.
4. Per le ore indica la fascia richiesta. I permessi orari ordinari riguardano una giornata e devono rientrare nell’orario lavorativo; la pausa non viene conteggiata come lavoro.
5. Completa i campi specifici della categoria.
6. Premi **Calcular** e verifica periodo, quantità e informazioni sul cupo.
7. Premi **Presentar solicitud**.
8. Controlla il record in **Historial de permisos** e apri **Detalle**.

Il menu propone le categorie attive pubblicate dall’amministrazione globale. Non è un semplice campo libero: il motivo spiega il caso, ma non sostituisce la categoria e i requisiti. Se non trovi il tipo corretto, chiedi a HR prima di scegliere una categoria diversa.

I permessi non consumano il saldo delle ferie. La loro remunerazione è descritta nella regola; l’applicazione non esegue il calcolo della busta paga o dei sussidi.

## 7 Campi che dipendono dal permesso

Compila soltanto i campi proposti per il tipo scelto, con i dati reali richiesti.

| Caso | Informazioni da controllare |
|---|---|
| Matrimonio o lutto | Data dell’evento e, per lutto, parentela; eventuali ulteriori date per giornate non consecutive |
| Paternità, maternità o adozione | Evento o provvedimento, periodo e dati specifici richiesti dal modulo |
| Maternità con estensioni o trasferimento | Supuesto applicabile e dati/documenti che lo sostengono; chiedi assistenza a HR se il caso è complesso |
| Lattazione ricorrente | Periodo, dati richiesti e durata giornaliera; verifica l’intero intervallo |
| Controlli o esami | Tipo e dati richiesti dal modulo, con documentazione pertinente |
| Obbligo legale o accompagnamento | Riferimento dell’obbligo o dell’assistenza, senza diagnosi nel motivo |
| Ricerca di lavoro durante preavviso | Periodo e modalità già verificati da HR |
| Categoria con limite per evento | Data che identifica l’evento, usata per il controllo del cupo |

Non frazionare lo stesso evento in richieste artificialmente separate per superare il limite: il sistema verifica i consumi pertinenti della stessa persona, comprese le richieste pendenti. Le durate e i requisiti effettivi sono quelli mostrati dalla categoria vigente alla presentazione.

Se scegli **Comunicar reposo médico**, il collegamento porta a **Mis ausencias**: il riposo medico viene dichiarato nel relativo flusso, senza creare un secondo permesso.

## 8 Allegare documenti a un permesso

1. Presenta la richiesta e apri **Historial de permisos → Detalle**.
2. Nel riquadro **Documentación privada** seleziona il documento.
3. Usa PDF, PNG o JPEG, entro 5 MB.
4. Attendi il messaggio di caricamento e controlla nome e stato antivirus nell’elenco.
5. Se HR richiede integrazioni, carica i documenti pertinenti sulla stessa richiesta.

Un documento caricato non equivale a un permesso approvato. Per i diritti legali protetti HR verifica i requisiti: nel dettaglio puoi leggere esito, commenti e un eventuale termine di consegna successivo. **PENDING** indica verifica aperta, **VERIFIED** requisiti verificati e **NOT_MET** requisiti non soddisfatti; lo stato della richiesta resta distinto da quello della verifica.

## 9 Consultare o annullare un permesso

In **Historial de permisos** controlla tipo, intervallo, quantità, stato e livello. **Detalle** mostra il circuito con precedenti decisioni e la documentazione della richiesta.

Se è disponibile **Cancelar solicitud**, inserisci l’eventuale motivo e premi il pulsante. L’annullamento conserva lo storico e libera i consumi pertinenti del permesso. Dipende dall’impostazione di cancellazione del gruppo; se non è disponibile, contatta HR.

Per un errore nelle date o nel tipo non inviare richieste duplicate: verifica prima con HR come annullare o sostituire quella attiva.

## 10 Dichiarare un’assenza

1. Apri **Mis ausencias**.
2. Inserisci **Fecha inicial** e **Fecha final**.
3. Seleziona **Tipo declarado**: **Ausencia medica**, **Ausencia injustificada** o **Otra ausencia justificada**.
4. Compila **Motivo o comentario** con le informazioni necessarie.
5. Premi **Registrar declaracion**.
6. Controlla il nuovo record in **Historial propio**.

La dichiarazione non rende automaticamente giustificata l’assenza. HR esamina il caso e registra classificazione e stato finali. Il giustificativo dell’assenza viene caricato da HR: consegnalo al referente aziendale previsto, senza cercare un comando di caricamento in questa schermata.

| Stato dell’assenza | Significato |
|---|---|
| Declarada / Pendiente de revision | Il caso attende la revisione |
| Justificada | HR ha registrato la giustificazione |
| Injustificada | HR ha registrato l’esito ingiustificato |
| Cancelada | Il record è stato annullato conservando lo storico |

In caso di errore avvisa HR. Nell’autoservizio delle assenze non è previsto un pulsante per modificare o annullare liberamente il record. Se compare un conflitto con ferie o permessi, chiedi assistenza invece di creare altre dichiarazioni con date alterate.

## 11 Notifiche e assistenza

Le email possono avvisarti di presentazione e decisioni. Per sapere se la richiesta è stata effettivamente salvata o decisa, controlla sempre l’applicazione. La mancata consegna di un’email non annulla l’operazione.

| Problema | A chi rivolgersi o cosa fare |
|---|---|
| Invito scaduto o account inattivo | Amministratore tenant |
| Dati personali o rapporto errati | Amministratore tenant |
| Nessuna giornata lavorativa disponibile | Chiedi verifica di orario e calendario |
| Saldo ferie insufficiente o inatteso | Confronta disponibili e riservati; contatta HR |
| Categoria mancante o dubbio sui requisiti | HR |
| Documento respinto | Controlla formato e dimensione; segnala l’eventuale errore antivirus |
| Richiesta ferma al medesimo livello | Comunica a HR tipo, periodo e livello indicato |
| Il pulsante per annullare manca | Chiedi a HR la gestione del caso |

Nella segnalazione indica legajo, funzione, date e messaggio visualizzato. Non comunicare la password. Se hai anche altri ruoli, usa comunque l’autoservizio per le tue richieste personali: il ruolo operativo non consente di approvarle da solo.
