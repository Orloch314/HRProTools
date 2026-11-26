# HR ProTools Manuale amministratore globale

Edizione del 23 novembre 2026. Destinatario: **Administrador Global**, ruolo `GlobalAdministrator`.

L’amministratore globale crea i gruppi aziendali, configura la posta e mantiene i cataloghi comuni di permessi legali e festività ufficiali. Questo manuale usa spiegazioni in italiano e le etichette spagnole dell’interfaccia. I percorsi si aggiungono all’indirizzo abituale di HR ProTools.

## 1 Accesso e responsabilità

Accedi da **Iniciar sesión** con l’utenza globale predisposta per te. Usa **¿Olvidó su contraseña?** se devi recuperare l’accesso e **Cerrar sesion** al termine. La password applicativa richiede almeno 12 caratteri, con maiuscola, minuscola, numero e simbolo.

Il ruolo globale non sostituisce gli operatori dei singoli gruppi: le richieste dei dipendenti, le approvazioni, le assenze e gli accessi tenant seguono i relativi ruoli e ambiti. Le modifiche ai cataloghi comuni possono invece incidere sui nuovi calcoli di tutti i gruppi.

| Menu | Percorso |
|---|---|
| Grupos empresariales | `/admin/grupos` |
| Correo SMTP | `/admin/smtp` |
| Permisos legales | `/admin/permisos-legales` |
| Feriados oficiales | `/admin/feriados-oficiales` |

## 2 Configurare la posta

Apri **Correo SMTP**. Compila i parametri forniti dal servizio di posta: **Servidor**, **Puerto**, **Seguridad**, **Usuario**, **Contraseña**, **Email remitente** e **Nombre remitente**. Scegli la modalità di sicurezza corrispondente al servizio e premi **Guardar configuracion**.

Se è già indicata una password **guardada**, lascia il campo vuoto per mantenerla; inserisci un nuovo valore soltanto per sostituirla. Il salvataggio della configurazione non costituisce una prova di consegna: verifica la ricezione durante un invito autorizzato in ambiente di prova.

Inviti non ricevuti richiedono il controllo dell’indirizzo destinatario, dello spam e del servizio di posta. Per problemi di dominio, certificato o collegamenti di invito che puntano a un indirizzo errato, coinvolgi il referente tecnico. I tenant possono controllare e ritentare le notifiche fallite del proprio circuito, ma non modificano il server SMTP globale.

## 3 Creare un gruppo aziendale

1. Apri **Grupos empresariales** e premi **Nuevo grupo**.
2. Inserisci **Codigo del grupo** e **Nombre del grupo**. Il codice del gruppo è compilato dall’operatore.
3. Inserisci **Razon social** e **RUC** della prima impresa.
4. Indica nome ed email dell’**Administrador tenant**.
5. Controlla i dati e premi **Crear grupo e invitar** una sola volta.
6. Attendi l’esito e verifica il gruppo in **Grupos registrados**.
7. Fai completare l’attivazione al destinatario dell’invito.

L’operazione prepara il gruppo, il relativo spazio dati, la prima impresa e l’account amministrativo. Il codice dell’impresa è assegnato dal sistema. Se appare **Invitacion preparada** con un collegamento di prova, consegnalo soltanto al destinatario previsto e verifica la configurazione della posta.

La tabella mostra codice, gruppo, schema, stato e aggiornamento. Se compare un errore di preparazione, annota il messaggio e coinvolgi il referente tecnico prima di tentare una nuova creazione: non creare duplicati per aggirare un errore.

L’admin tenant completa poi organizzazione, orari, dipendenti e circuito secondo il [manuale amministratore tenant](Manuale-01-Admin-Tenant.md). In questa schermata non sono previste operazioni generali di eliminazione o modifica dei gruppi già creati.

## 4 Consultare i permessi legali

Apri **Permisos legales**. Usa **Buscar** per codice o nome e il filtro di stato della **última versión**. Premi:

- **Ficha** per leggere caratteristiche e requisiti della versione elencata;
- **Historial** per consultare le versioni con vigenze, motivo, data e autore;
- **Editar** per preparare una nuova versione;
- **Activar** o **Desactivar** per preparare la variazione di stato.

Il catalogo è comune ai tenant. Le richieste già presentate conservano la versione applicata e non vengono ricalcolate quando pubblichi una modifica. Il filtro globale mostra l’ultima versione pubblicata, che può avere una decorrenza futura; controlla sempre **Vigencia desde**.

## 5 Pubblicare una categoria parametrizzabile

1. Premi **Nuevo permiso**.
2. Inserisci un codice unico in maiuscolo, con lettere, numeri o underscore, e compila nome, gruppo e descrizione.
3. Indica legge, articolo e URL HTTPS della fonte normativa.
4. Imposta **Vigencia desde** e, se applicabile, **Vigencia hasta**. Queste date governano la versione utilizzabile alla presentazione.
5. Seleziona l’unità e inserisci la durata descrittiva.
6. Compila il **Máximo por solicitud**, obbligatorio per una nuova categoria semplice, ed eventuali limiti per evento, mese e anno.
7. Specifica beneficiari, requisiti, documentazione, preavviso e regime di remunerazione.
8. Scegli natura obbligatoria o discrezionale, **Goce** e stato attivo/inattivo.
9. Scrivi il motivo della pubblicazione e premi **Publicar versión**.
10. Riapri la ficha e controlla quanto pubblicato.

### Interpretare le unità

| Unità nel modulo | Criterio applicato |
|---|---|
| Días laborables | Giorni lavorabili dell’orario e del calendario integrato |
| Horas laborables | Ore effettive entro la giornata, al netto della pausa |
| Minutos laborables | Minuti effettivi della fascia richiesta |
| Semanas consecutivas | Settimane complete di calendario, compresi riposi e festività |
| Días u horas | Giorni o frazioni della giornata lavorativa; limiti espressi in giorni |

La durata testuale descrive il diritto; i campi numerici regolano i limiti delle categorie semplici. Un limite vuoto non introduce un tetto per quella voce. Con limite per evento, il dipendente deve indicare la data che identifica l’evento. Pendenti e approvati della stessa persona contribuiscono al consumo del relativo cupo; il rigetto o l’annullamento lo liberano secondo lo stato.

**Obligatoria** comporta verifica dei requisiti da HR; **Discrecional** usa il circuito ordinario. I requisiti descrittivi richiedono la verifica dell’operatore: non sono un linguaggio per programmare condizioni automatiche. I campi sul goce non eseguono payroll, trattenute o calcoli di sussidi.

## 6 Modificare o disattivare una categoria

Premi **Editar**, controlla la nuova decorrenza, modifica i campi consentiti, indica il motivo e pubblica. Il codice resta stabile. Per cambiare l’unità di una categoria parametrizzata occorre una nuova categoria, così da mantenere coerenti i consumi storici.

**Activar** e **Desactivar** aprono il modulo con lo stato desiderato: completa decorrenza e motivo, poi premi **Publicar versión**. La categoria non viene eliminata; dalla decorrenza di disattivazione smette di essere proposta per nuove presentazioni. Le richieste precedenti, i documenti e le decisioni restano consultabili.

### Regole specializzate

Le licenze con logiche specifiche, tra cui maternità, lattazione, accompagnamento di disabilità, controlli prenatali e ricerca di lavoro durante preavviso, conservano la propria logica. Il pannello consente di versionare i dati descrittivi e lo stato, ma blocca variazioni incompatibili di durata, unità, natura o limiti.

Se la modifica è respinta come non supportata, trasmetti al referente tecnico norma, decorrenza e comportamento atteso. Non alterare soltanto il testo della durata per far apparire applicata una regola numerica diversa. Le categorie espressamente escluse dal progetto non possono essere pubblicate tramite questo pannello.

## 7 Consultare il calendario ufficiale

Apri **Feriados oficiales** e seleziona l’anno. Puoi consultare calendario annuale e lista cronologica, con nome, data originale, data effettiva, tipo, stato, fonte e ultima modifica.

**Fecha original / conmemorativa** identifica la ricorrenza; **Fecha efectiva** è la data usata per il computo lavorativo. La conservazione di entrambe non crea due giorni festivi. Le date evidenziate nel calendario corrispondono ai record attivi.

Controlla lo stato **confirmado** oppure **incompleto / pendiente de confirmación**. Alla data di questa edizione il 2026 è caricato come base documentata ma resta da confermare nella sua integrità; gli altri anni non vengono copiati automaticamente dal 2026.

## 8 Aggiungere una festività

1. Premi **Agregar feriado**.
2. Inserisci nome, data originale e data effettiva; l’anno di osservanza deriva dalla data effettiva.
3. Scegli **Nacional**, **Trasladado** o **Extraordinario**.
4. Compila fondamento legale, fonte HTTPS e, quando necessaria, disposizione o decreto.
5. Indica la classe di cambio **Alta inicial**, il motivo e lo stato.
6. Premi **Guardar revisión** e verifica la lista dell’anno interessato.

Il salvataggio rende il dato operativo per i nuovi calcoli e riporta l’anno allo stato pendente di verifica. Non usare una notizia non verificata come sostituto della disposizione applicabile.

## 9 Cambiare una data o correggere un errore

1. Sulla riga esistente premi **Editar / cambiar fecha**.
2. Conserva o correggi la data originale secondo il documento di riferimento e indica la nuova data effettiva.
3. Scegli **Traslado oficial** se registri un trasferimento disposto dall’autorità, oppure **Corrección administrativa** se correggi un errore di inserimento.
4. Registra fonte, disposizione quando richiesta e motivo preciso.
5. Salva e consulta **Historial** per verificare entrambe le revisioni.

La data effettiva è modificabile anche per una festività tradizionalmente fissa. Questa possibilità tecnica non sostituisce la verifica della disposizione. Una correzione interna non va presentata come decreto ufficiale.

Per un feriado trasferito o straordinario mantieni la disposizione pertinente anche quando registri una successiva correzione amministrativa. Per disattivare un record errato usa lo stato **Inactivo** in una nuova revisione, conservando il motivo.

## 10 Confermare un anno

Apri **Verificar integridad del calendario anual**, inserisci fonte documentale e risultato della verifica dell’intero anno, seleziona **Confirmado** o **Pendiente / incompleto** e premi **Guardar verificación**.

La conferma è una dichiarazione amministrativa di completezza, non una ricerca automatica delle norme. Ogni revisione successiva di una festività rende nuovamente necessaria la verifica. Prepara gli anni futuri separatamente: non vengono creati trasferimenti automatici.

## 11 Effetto sui tenant e sulle richieste

I tenant vedono i dati ufficiali in sola lettura e possono aggiungere soltanto i propri giorni aziendali. Se un giorno ufficiale coincide con uno aziendale, entrambi restano identificati ma il computo considera una sola data.

Le nuove richieste usano il calendario aggiornato. Quelle già presentate mantengono date, riserve, consumi e approvazioni originali. Il calendario tenant segnala le richieste attive con date future che conservano indicatori di festività diversi dal calendario attuale: comunica la modifica agli amministratori interessati affinché i casi siano esaminati individualmente.

## 12 Problemi frequenti

| Messaggio o situazione | Operazione |
|---|---|
| La versione è cambiata | Riapri il record e prepara la modifica sull’ultima versione |
| Configurazione non supportata | Controlla unità, limiti e natura; per una regola specialistica coinvolgi il referente tecnico |
| Vigencia non accettata | Non retrocedere rispetto all’ultima versione della categoria |
| Fonte non valida | Inserisci un URL HTTPS completo senza credenziali incorporate |
| Calendario pendente dopo una modifica | Verifica nuovamente l’integrità annuale e registra l’esito |
| Invito non ricevuto | Controlla destinatario e SMTP; non creare nuovamente lo stesso gruppo |
| Gruppo con errore di preparazione | Conserva il messaggio e chiedi verifica tecnica |

Il ruolo globale non offre un pannello per cambiare le basi delle ferie dei dipendenti. L’amministratore tenant configura gli eventuali giorni aggiuntivi aziendali nella propria sezione **Políticas de vacaciones**.
