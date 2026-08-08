// de — German.
//
// Edit: the trade words are the ones a German Autohaus actually uses. A
//       dealership is an *Autohaus*, not a "Händlerschaft"; stock is
//       *Fahrzeugbestand*; a lead is an *Anfrage*. Accounting follows HGB
//       usage — *Summen- und Saldenliste* is the trial balance, and
//       *Buchungsperiode* is the period that gets closed.
//
//       German compounds run long, and the shell's navigation is the tightest
//       space in the application. Where the full term does not fit, the short
//       form goes in `nav.*` and the full one in the page heading — the same
//       word in both places would either overflow the bar or under-explain the
//       screen. `Saldenliste` in the bar, `Summen- und Saldenliste` on the page.
//
//       Formal *Sie* throughout. This is software somebody uses at work.

import type { Catalogue } from '../index';

export const de: Catalogue = {
  'app.name': 'DealerFOSS',
  'common.loading': 'Wird geladen…',
  'common.save': 'Speichern',
  'common.saving': 'Wird gespeichert…',
  'common.cancel': 'Abbrechen',
  'common.close': 'Schließen',
  'common.retry': 'Erneut versuchen',
  'common.search': 'Suchen',
  'common.searching': 'Wird gesucht…',
  'common.none': 'Keine',
  'common.all': 'Alle',
  'common.yes': 'Ja',
  'common.no': 'Nein',
  'common.back': 'Zurück',
  'common.continue': 'Weiter',
  'common.unexpected': 'Etwas ist schiefgelaufen. Versuchen Sie es erneut.',
  'common.notPermitted': 'Sie haben keine Berechtigung, dies zu sehen.',
  'common.unreachable': 'Der Server ist nicht erreichbar. Läuft er?',

  'error.network': 'Der Server ist nicht erreichbar. Läuft er?',
  'error.invalidCredentials':
    'Diese E-Mail-Adresse und dieses Passwort gehören zu keinem Konto.',
  'error.sessionRequired': 'Melden Sie sich an, um dies zu nutzen.',
  'error.adminSessionRequired':
    'Melden Sie sich als Administrator an, um die Verwaltungskonsole zu nutzen. Eine Autohaus-Anmeldung erreicht sie nicht.',
  'error.sessionInvalid': 'Ihre Sitzung ist beendet. Melden Sie sich erneut an.',
  'error.antiForgeryFailed':
    'Dieser Browser hat kein Token mehr, das zu seiner Sitzung passt. Melden Sie sich erneut an.',
  'error.secondFactorRejected': 'Dieser Code wurde nicht akzeptiert.',
  'error.secondFactorRequired':
    'Ihre Rolle verlangt die Anmeldung in zwei Schritten. Richten Sie sie ein, um die übrige Anwendung zu erreichen.',
  'error.mfaNotEnrolled': 'Für dieses Konto ist die Anmeldung in zwei Schritten nicht eingerichtet.',
  'error.mfaAlreadyOn': 'Für dieses Konto ist die Anmeldung in zwei Schritten bereits aktiv.',
  'error.notATenantCaller': 'Ein Administrator kann nicht als Autohaus-Benutzer handeln.',
  'error.tenantRequired': 'Geben Sie an, um welche Händlergruppe es sich handelt.',
  'error.tenantNotFound': 'Keine aktive Händlergruppe trägt diesen Namen.',

  'shell.skipToContent': 'Zum Inhalt springen',
  'shell.mainNavigation': 'Hauptnavigation',
  'shell.signOut': 'Abmelden',
  'shell.shortcuts': 'Tastenkürzel',
  'shell.shortcutsTitle': 'Tastenkürzel ( ? )',
  'shell.language': 'Sprache',
  'shell.appearance': 'Darstellung',

  'nav.dashboard': 'Dieser Monat',
  'nav.customers': 'Kunden',
  'nav.leads': 'Anfragen',
  'nav.deals': 'Verkäufe',
  'nav.stock': 'Bestand',
  'nav.workshop': 'Werkstatt',
  'nav.parts': 'Teile',
  'nav.trialBalance': 'Saldenliste',
  'nav.books': 'Die Bücher',
  'nav.records': 'Datensätze',
  'nav.staff': 'Mitarbeiter',
  'nav.secondFactor': 'Anmeldung in zwei Schritten',

  'appearance.auto': 'Auto',
  'appearance.autoHint': 'Diesem Gerät folgen',
  'appearance.light': 'Hell',
  'appearance.lightHint': 'Immer hell',
  'appearance.dark': 'Dunkel',
  'appearance.darkHint': 'Immer dunkel',

  'signIn.lede': 'Melden Sie sich bei Ihrem Autohaus an.',
  'signIn.dealerGroup': 'Händlergruppe',
  'signIn.email': 'E-Mail',
  'signIn.password': 'Passwort',
  'signIn.submit': 'Anmelden',
  'signIn.submitting': 'Anmeldung läuft…',
  'signIn.codeLede':
    'Geben Sie den sechsstelligen Code aus Ihrer Authenticator-App ein oder einen Ihrer Wiederherstellungscodes.',
  'signIn.code': 'Code',
  'signIn.checking': 'Wird geprüft…',
  'signIn.startAgain': 'Von vorn beginnen',

  'setPassword.title': 'Legen Sie Ihr Passwort fest',
  'setPassword.lede':
    'Ihre Führungskraft hat Ihnen einen Code gegeben. Verwenden Sie ihn hier einmalig, um ein Passwort zu wählen, das nur Sie kennen — niemand im Autohaus kann sehen, wofür Sie sich entscheiden.',
  'setPassword.dealership': 'Autohaus',
  'setPassword.email': 'E-Mail',
  'setPassword.code': 'Code',
  'setPassword.password': 'Neues Passwort',
  'setPassword.passwordHint':
    'Mindestens 12 Zeichen. Die Länge ist es, die ein Passwort schwer erratbar macht.',
  'setPassword.again': 'Neues Passwort wiederholen',
  'setPassword.mismatch': 'Die beiden stimmen nicht überein.',
  'setPassword.failed': 'Das hat nicht funktioniert.',
  'setPassword.submit': 'Mein Passwort festlegen',
  'setPassword.doneTitle': 'Sie sind eingerichtet',
  'setPassword.doneLede':
    'Melden Sie sich mit Ihrer E-Mail-Adresse und dem soeben gewählten Passwort an.',
  'setPassword.toSignIn': 'Zur Anmeldung',

  'secondFactor.title': 'Anmeldung in zwei Schritten',
  'secondFactor.required':
    'Ihr Autohaus verlangt für Ihre Rolle die Anmeldung in zwei Schritten. Bis Sie sie eingerichtet haben, ist dies der einzige Bildschirm, den Sie nutzen können.',
  'secondFactor.intro':
    'Danach fragt die Anmeldung zusätzlich zu Ihrem Passwort nach einem sechsstelligen Code aus einer App auf Ihrem Telefon. Google Authenticator, Authy und 1Password funktionieren alle.',
  'secondFactor.start': 'Beginnen',
  'secondFactor.starting': 'Wird vorbereitet…',
  'secondFactor.pointApp': 'Richten Sie Ihre Authenticator-App auf dieses Quadrat.',
  'secondFactor.qrTitle': 'Scannen Sie dies mit Ihrer Authenticator-App',
  'secondFactor.cannotScan': 'Lässt es sich nicht scannen?',
  'secondFactor.typeInstead': 'Geben Sie stattdessen dies von Hand in die App ein:',
  'secondFactor.enterCode': 'Geben Sie nun den angezeigten Code ein',
  'secondFactor.turnOn': 'Einschalten',
  'secondFactor.checking': 'Wird geprüft…',
  'secondFactor.notYet':
    'An der Anmeldung hat sich noch nichts geändert. Sie greift erst, sobald der Code oben akzeptiert wurde.',
  'secondFactor.onNow':
    'Die Anmeldung in zwei Schritten ist aktiv. Von jetzt an werden Sie nach Ihrem Passwort nach einem Code gefragt.',
  'secondFactor.saveTitle': 'Bewahren Sie diese sicher auf',
  'secondFactor.saveLede':
    'Jeder davon funktioniert einmal, und nur wenn Sie Ihr Telefon verlieren. Dies ist das einzige Mal, dass sie angezeigt werden.',
  'secondFactor.recoveryCodes': 'Wiederherstellungscodes',

  'shortcuts.title': 'Tastenkürzel',
  'shortcuts.space': 'Leertaste',
  'shortcuts.note':
    'Tastenkürzel werden ignoriert, während Sie in ein Feld schreiben — so verschlucken sie nie ein Zeichen, das Sie tippen wollten.',
  'shortcuts.goDashboard': 'Zur Übersicht',
  'shortcuts.goStock': 'Zum Bestand',
  'shortcuts.goCustomers': 'Zu den Kunden',
  'shortcuts.goLeads': 'Zu den Anfragen',
  'shortcuts.goDeals': 'Zu den Verkäufen',
  'shortcuts.goWorkshop': 'Zur Werkstatt',
  'shortcuts.goParts': 'Zu den Teilen',
  'shortcuts.goBooks': 'Zu den Büchern',
  'shortcuts.monthBefore': 'In der Übersicht: der Monat davor',
  'shortcuts.monthAfter': 'In der Übersicht: der Monat danach',
  'shortcuts.thisMonth': 'In der Übersicht: zurück zu diesem Monat',
  'shortcuts.showList': 'Diese Liste anzeigen',

  // Werkstatt- und Handelsvokabular: „Aufbereitung“ ist der Begriff für das
  // Herrichten eines Gebrauchtwagens, und ein für einen Verkauf zurückgelegtes
  // Fahrzeug ist „reserviert“, nicht „wartend“.
  'enum.inventoryStatus.Incoming': 'Im Zulauf',
  'enum.inventoryStatus.Reconditioning': 'In Aufbereitung',
  'enum.inventoryStatus.Available': 'Verfügbar',
  'enum.inventoryStatus.OnHold': 'Reserviert',
  'enum.inventoryStatus.Sold': 'Verkauft',
  'enum.inventoryStatus.Removed': 'Entfernt',

  'enum.leadStatus.New': 'Neu',
  'enum.leadStatus.Working': 'In Bearbeitung',
  'enum.leadStatus.Appointment': 'Termin',
  'enum.leadStatus.Won': 'Gewonnen',
  'enum.leadStatus.Lost': 'Verloren',

  'enum.leadSource.WalkIn': 'Laufkundschaft',
  'enum.leadSource.Phone': 'Telefon',
  'enum.leadSource.Website': 'Website',
  'enum.leadSource.Referral': 'Empfehlung',
  'enum.leadSource.Marketplace': 'Marktplatz',
  'enum.leadSource.Unknown': 'Unbekannt',

  'enum.dealStatus.Draft': 'Entwurf',
  'enum.dealStatus.Submitted': 'Eingereicht',
  'enum.dealStatus.Approved': 'Genehmigt',
  'enum.dealStatus.Delivered': 'Ausgeliefert',
  'enum.dealStatus.Lost': 'Verloren',

  'enum.chargeKind.VehiclePrice': 'Fahrzeugpreis',
  'enum.chargeKind.Fee': 'Gebühr',
  'enum.chargeKind.Discount': 'Nachlass',
  'enum.chargeKind.Accessory': 'Zubehör',

  'enum.repairOrderStatus.Booked': 'Terminiert',
  'enum.repairOrderStatus.InProgress': 'In Arbeit',
  'enum.repairOrderStatus.Completed': 'Abgeschlossen',
  'enum.repairOrderStatus.Invoiced': 'Berechnet',
  'enum.repairOrderStatus.Cancelled': 'Storniert',

  'enum.serviceLineKind.Labour': 'Arbeitslohn',
  'enum.serviceLineKind.Part': 'Teil',
  'enum.serviceLineKind.Sublet': 'Fremdleistung',

  'enum.financeProductKind.Warranty': 'Garantie',
  'enum.financeProductKind.Gap': 'GAP',
  'enum.financeProductKind.ServicePlan': 'Servicevertrag',
  'enum.financeProductKind.Protection': 'Schutzpaket',
  'enum.financeProductKind.Other': 'Sonstiges',

  'enum.periodState.Open': 'Offen',
  'enum.periodState.Closed': 'Abgeschlossen',

  'enum.booksState.NotOpened': 'Nicht eröffnet',
  'enum.booksState.Open': 'Offen',
  'enum.booksState.Closed': 'Abgeschlossen',
  'enum.booksState.Unknown': 'Unbekannt',

  'enum.importKind.Customers': 'Kunden',
  'enum.importKind.Vehicles': 'Fahrzeuge',

  'enum.tenantStatus.Active': 'Aktiv',
  'enum.tenantStatus.Suspended': 'Ausgesetzt',
  'enum.tenantStatus.Provisioning': 'Wird eingerichtet',
  'enum.tenantStatus.Archived': 'Archiviert',

  'stock.title': 'Fahrzeugbestand',
  'stock.status': 'Status',
  'stock.loading': 'Bestandsliste wird geladen…',
  'stock.denied':
    'Sie haben keinen Zugriff auf den Bestand dieses Standorts. Fragen Sie eine Führungskraft, falls das nicht stimmen sollte.',
  'stock.failed': 'Die Bestandsliste konnte nicht geladen werden.',
  'stock.empty':
    'Noch nichts vorhanden. Fahrzeuge erscheinen, sobald sie in den Bestand aufgenommen wurden.',
  'stock.onlyStockNumber': 'Es wird nur Bestandsnummer {stock} angezeigt.',
  'stock.showEverything': 'Alles anzeigen',
  'stock.colStock': 'Bestandsnr.',
  'stock.colVehicle': 'Fahrzeug',
  'stock.colVin': 'FIN',
  'stock.colStatus': 'Status',
  'stock.count': { one: '{count} Fahrzeug im Bestand', other: '{count} Fahrzeuge im Bestand' },
  'stock.countCapped': 'Die ersten {count} Fahrzeuge im Bestand. Es können mehr sein.',
  'stock.cappedNote':
    'Es werden die ersten {count} angezeigt. Es können mehr sein — grenzen Sie es mit dem Statusfilter ein, bis es eine Seitenblätterung gibt.',

  // HGB-Bilanzgliederung.
  'enum.accountKind.Asset': 'Aktiva',
  'enum.accountKind.Liability': 'Passiva',
  'enum.accountKind.Equity': 'Eigenkapital',
  'enum.accountKind.Revenue': 'Erträge',
  'enum.accountKind.Expense': 'Aufwendungen',

  'trialBalance.title': 'Summen- und Saldenliste',
  'trialBalance.loading': 'Wird zusammengerechnet…',
  'trialBalance.denied': 'Sie haben keinen Zugriff auf diese Zahlen.',
  'trialBalance.failed': 'Die Salden konnten nicht geladen werden.',
  'trialBalance.empty':
    'Noch nichts gebucht. Buchungen erscheinen hier, sobald ein Fahrzeug ausgeliefert wurde.',
  'trialBalance.inBalance': 'Ausgeglichen — Soll und Haben belaufen sich beide auf {total}.',
  'trialBalance.outOfBalance':
    'Differenz von {difference}. Auf dem Weg ist etwas verloren gegangen.',
  'trialBalance.colCode': 'Kontonummer',
  'trialBalance.colAccount': 'Konto',
  'trialBalance.colKind': 'Art',
  'trialBalance.colDebits': 'Soll',
  'trialBalance.colCredits': 'Haben',
  'trialBalance.colBalance': 'Saldo',
  'trialBalance.total': 'Summe',

  'enum.customerKind.Person': 'Privatperson',
  'enum.customerKind.Business': 'Firma',

  'customers.title': 'Kunden',
  'customers.find': 'Jemanden suchen',
  'customers.findPlaceholder': 'Name, Telefon oder E-Mail',
  'customers.add': 'Kunden anlegen',
  'customers.looking': 'Wird gesucht…',
  'customers.denied':
    'Sie haben keinen Zugriff auf Kundendaten. Fragen Sie eine Führungskraft, falls das nicht stimmen sollte.',
  'customers.failed': 'Die Kunden konnten nicht geladen werden.',
  'customers.noMatches': 'Dazu passt niemand.',
  'customers.colName': 'Name',
  'customers.colKind': 'Art',
  'customers.colEmail': 'E-Mail',
  'customers.colPhone': 'Telefon',
  'customers.count': { one: '{count} Kunde', other: '{count} Kunden' },
  'customers.countCapped': 'Die ersten {count} Kunden. Es können mehr sein.',
  'customers.cappedNote':
    'Es werden die ersten {count} angezeigt. Es können mehr sein — grenzen Sie die Suche ein, bis es eine Seitenblätterung gibt.',

  'customers.kindLabel': 'Privatperson oder Firma',
  'customers.firstName': 'Vorname',
  'customers.lastName': 'Nachname',
  'customers.businessName': 'Firmenname',
  'customers.email': 'E-Mail',
  'customers.phone': 'Telefon',
  'customers.submit': 'Anlegen',
  'customers.checking': 'Suche nach Dubletten…',
  'customers.adding': 'Wird angelegt…',

  'customers.duplicateTitle': 'Jemanden wie diesen gibt es bereits',
  'customers.duplicateLede':
    'Ein zweiter Datensatz für dieselbe Person zerteilt ihre Historie — Werkstatt, Verkäufe und Kontaktdaten stimmen dann nicht mehr überein. Prüfen Sie, ob eine dieser Personen gemeint ist.',
  'customers.noContactDetails': 'keine Kontaktdaten',
  'customers.oneOfTheseIsThem': 'Eine davon ist es',
  'customers.addAnyway': 'Keine davon — trotzdem anlegen',

  'enum.importOutcome.Pending': 'Ausstehend',
  'enum.importOutcome.Created': 'Angelegt',
  'enum.importOutcome.Updated': 'Bereits vorhanden',
  'enum.importOutcome.Skipped': 'Übersprungen',
  'enum.importOutcome.Failed': 'Abgelehnt',

  'records.title': 'Datensätze',
  'records.bringIn': 'Datensätze einlesen',
  'records.bringInLede':
    'Eine Tabelle, die Sie aus Ihrem alten System als CSV exportiert haben. Es wird nichts geschrieben, bevor Sie einen Probelauf gemacht haben.',
  'records.whatIsInIt': 'Was in der Datei steht',
  'records.file': 'Die Datei',
  'records.unreadableFile': 'Diese Datei konnte nicht gelesen werden. Ist es eine Text-CSV?',
  'records.practice': 'Probelauf',
  'records.practising': 'Wird ausprobiert…',
  'records.importForReal': 'Echt importieren',
  'records.importing': 'Wird importiert…',
  'records.practiseFirst':
    'Machen Sie zuerst den Probelauf. Er ändert nichts und zeigt Ihnen genau, was der echte Lauf tun wird.',

  'records.takeOut': 'Datensätze herausgeben',
  'records.takeOutLede':
    'Lädt alles dieser Art als CSV herunter. Es ist dieselbe Struktur, die diese Seite wieder annimmt — Sie können Ihre Daten also überallhin mitnehmen, auch in ein ganz anderes System.',
  'records.downloadCustomers': 'Kunden herunterladen',
  'records.downloadVehicles': 'Fahrzeuge herunterladen',

  'records.couldNotRun': 'Dieser Import konnte nicht ausgeführt werden.',
  'records.whatWouldHappen': 'Was passieren würde',
  'records.whatHappened': 'Was passiert ist',
  'records.summaryPractice': {
    one: 'Von {count} Zeile: {created} würden angelegt, {updated} bereits vorhanden, {skipped} übersprungen, {failed} nicht lesbar.',
    other:
      'Von {count} Zeilen: {created} würden angelegt, {updated} bereits vorhanden, {skipped} übersprungen, {failed} nicht lesbar.',
  },
  'records.summaryReal': {
    one: 'Von {count} Zeile: {created} angelegt, {updated} bereits vorhanden, {skipped} übersprungen, {failed} abgelehnt.',
    other:
      'Von {count} Zeilen: {created} angelegt, {updated} bereits vorhanden, {skipped} übersprungen, {failed} abgelehnt.',
  },
  'records.nothingWritten': 'Es wurde nichts geschrieben. Das war ein Probelauf.',
  'records.rowsToLookAt': 'Zeilen, die Sie ansehen sollten',
  'records.rowsToLookAtLede':
    'Die Zeilennummer ist die, die Sie in Ihrer Tabelle sehen, und die Zeile ist genau so zitiert, wie sie ankam. Korrigieren Sie die Datei und starten Sie erneut — hier wird nichts an dem geändert, was Sie geschickt haben.',
  'records.problemCount': {
    one: '{count} Zeile, die Aufmerksamkeit braucht',
    other: '{count} Zeilen, die Aufmerksamkeit brauchen',
  },
  'records.colLine': 'Zeile',
  'records.colWhatHappened': 'Was passiert ist',
  'records.colTheRow': 'Die Zeile',
  'records.timeout':
    'Dieser Import dauert länger als erwartet. Er läuft weiterhin — nur diese Seite hat aufgehört zu warten.',
};
