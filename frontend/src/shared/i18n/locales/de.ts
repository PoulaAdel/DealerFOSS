// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   de — German.
//
// Usage:
//   Loaded by shared/i18n/index.tsx and selected by the language
//   picker. Never imported by a screen — a screen calls t(key), and
//   which catalogue answers is not its business.
//
// Coding Instructions:
//   The trade words are the ones a German Autohaus actually uses. A
//   dealership is an *Autohaus*, not a "Händlerschaft"; stock is
//   *Fahrzeugbestand*; a lead is an *Anfrage*. Accounting follows HGB
//   usage — *Summen- und Saldenliste* is the trial balance, and
//   *Buchungsperiode* is the period that gets closed.
//
//   German compounds run long, and the shell's navigation is the tightest
//   space in the application. Where the full term does not fit, the short
//   form goes in `nav.*` and the full one in the page heading — the same
//   word in both places would either overflow the bar or under-explain the
//   screen. `Saldenliste` in the bar, `Summen- und Saldenliste` on the page.
//
//   Formal *Sie* throughout. This is software somebody uses at work.

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

  'record.opening': 'Datensatz wird geöffnet…',
  'record.unreachable':
    'Dieser Datensatz kann nicht geöffnet werden. Möglicherweise wurde er entfernt, oder er gehört zu einem Bereich des Unternehmens, den Sie nicht sehen dürfen.',
  'record.backToList': 'Zurück zur Liste',

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
  'nav.passkeys': 'Passkeys',

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
  'enum.leadSource.Marketplace': 'Online-Anzeige',
  'enum.leadSource.Unknown': 'Nicht erfasst',

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

  'enum.servicePayType.CustomerPay': 'Kunde zahlt',
  'enum.servicePayType.Warranty': 'Garantie',
  'enum.servicePayType.Internal': 'Intern',

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
  'stock.detailFor': 'Bestandsnummer {stock}',
  'stock.cost': 'Kosten',
  'stock.costUnknown': 'nicht erfasst',
  'stock.acquired': 'Aufgenommen am',
  'stock.historyTitle': 'Was damit geschehen ist',
  'stock.historyEmpty': 'Zu diesem Fahrzeug wurde noch nichts erfasst.',
  'stock.takenIn': 'Als {to} in den Bestand aufgenommen',
  'stock.moved': '{from} → {to}',

  'nav.reports': 'Berichte',

  'reports.title': 'Was der Monat gebracht hat',
  'reports.month': 'Monat',
  'reports.loading': 'Zahlen werden ermittelt…',
  'reports.denied': 'Sie haben keinen Zugriff auf die Zahlen. Fragen Sie, wer die Bücher führt.',
  'reports.profitTitle': 'Gewinn- und Verlustrechnung',
  'reports.department': 'Abteilung',
  'reports.revenue': 'Erlöse',
  'reports.cost': 'Kosten',
  'reports.gross': 'Rohertrag',
  'reports.grossProfit': 'Rohertrag gesamt',
  'reports.overheads': 'Was der Betrieb kostet',
  'reports.totalOverheads': 'Summe der Kosten',
  'reports.netProfit': 'Ergebnis',
  'reports.sheetTitle': 'Was das Unternehmen wert ist',
  'reports.sheetBalances': 'Es stimmt: Was das Unternehmen besitzt entspricht dem, was es schuldet, plus seinem Wert.',
  'reports.sheetDoesNotBalance':
    'Das geht nicht auf. Es wurde etwas gebucht, das dieser Bericht nicht zuordnen kann — behandeln Sie alle Zahlen unten als fraglich, bis jemand herausgefunden hat, was.',
  'reports.assets': 'Was es besitzt',
  'reports.liabilities': 'Was es schuldet',
  'reports.equity': 'Was die Eigentümer eingebracht haben',
  'reports.total': 'Summe',
  'reports.nothingHere': 'Hier ist noch nichts erfasst.',
  'reports.earningsToDate': 'Seit Beginn erwirtschaftet',
  'reports.earningsNote':
    'Als eigene Zeile geführt und nicht dem Eingebrachten zugeschlagen, weil noch kein Jahr abgeschlossen wurde.',

  'entry.title': 'Buchung erfassen',
  'entry.note':
    'Für das, was sonst nichts erfasst: eine Ausgabe, eingebrachtes Kapital, eine Korrektur. Beide Seiten müssen dieselbe Summe ergeben.',
  'entry.whichLocation': 'Welcher Standort',
  'entry.chooseLocation': 'Standort wählen…',
  'entry.when': 'Wann es war',
  'entry.what': 'Wofür',
  'entry.account': 'Konto',
  'entry.chooseAccount': 'Konto wählen…',
  'entry.debit': 'Soll',
  'entry.credit': 'Haben',
  'entry.lineNote': 'Notiz',
  'entry.accountOnLine': 'Konto in Zeile {line}',
  'entry.debitOnLine': 'Soll in Zeile {line}',
  'entry.creditOnLine': 'Haben in Zeile {line}',
  'entry.noteOnLine': 'Notiz in Zeile {line}',
  'entry.totals': 'Summen',
  'entry.addLine': 'Zeile hinzufügen',
  'entry.outBy': 'Die beiden Seiten weichen um {amount} ab.',
  'entry.record': 'Buchen',

  'enum.paymentMethod.Cash': 'Bar',
  'enum.paymentMethod.Card': 'Karte',
  'enum.paymentMethod.BankTransfer': 'Überweisung',
  'enum.paymentMethod.Cheque': 'Scheck',
  'enum.paymentMethod.Finance': 'Finanzierungsgesellschaft',
  'enum.paymentMethod.CustomerCredit': 'Kundenguthaben',

  'money.title': 'Was noch offen ist',
  'money.billed': 'Berechnet',
  'money.paid': 'Bisher bezahlt',
  'money.outstanding': 'Noch offen',
  'money.settled': 'Vollständig bezahlt. Es ist nichts mehr offen.',
  'money.owedFor': {
    one: 'Seit {count} Tag offen.',
    other: 'Seit {count} Tagen offen.',
  },
  'money.paymentsTitle': 'Was bezahlt wurde',
  'money.howMuch': 'Betrag',
  'money.howPaid': 'Zahlungsart',
  'money.reference': 'Beleg (bleibt im Vorgang)',
  'money.takeIt': 'Zahlung erfassen',
  'picker.change': 'Ändern',
  'picker.typeToSearch': 'Zum Suchen tippen',
  'picker.searching': 'Suche läuft…',
  'picker.searchFailed': 'Die Suche konnte nicht ausgeführt werden. Erneut versuchen.',
  'picker.noMatches': 'Keine Treffer.',
  'picker.startTyping': 'Ein paar Buchstaben eingeben, um etwas zu finden.',
  'money.willOverpay':
    'Das sind {extra} mehr als geschuldet. Der Überschuss wird zu einem Guthaben des Kunden.',
  'money.creditUsable': 'Dieser Kunde hat Geld bei uns',
  'money.creditUsableNote':
    'Er hat früher zu viel gezahlt. Das kann mit dieser Rechnung verrechnet statt ausgezahlt werden.',
  'money.useItHere': '{amount} mit dieser Rechnung verrechnen',
  'money.creditTitle': 'Dem Kunden geschuldet',
  'money.creditNote':
    'Dieses Geld gehört ihm und liegt hier, bis es verwendet oder zurückgezahlt wird. Es gehört nicht dem Autohaus.',
  'money.creditFrom': 'zu viel gezahlt am {date}',
  'money.refundHow': 'Wie es zurückgeht',
  'money.giveItBack': 'Zurückzahlen',
  'stock.takeItIn': 'Fahrzeug in den Bestand nehmen',
  'stock.confirmTakeIn': 'Aufnehmen',
  'stock.takeInTitle': 'Fahrzeug in den Bestand nehmen',
  'stock.takeInNote':
    'Fahrzeug und Fahrzeugakte werden zusammen angelegt, denn ein ankommendes Auto ist fast immer eines, das Sie noch nie gesehen haben.',
  'stock.whichLocation': 'Welcher Standort',
  'stock.chooseLocation': 'Standort wählen…',
  'stock.vinOptional': 'FIN (optional)',
  'stock.modelYear': 'Baujahr',
  'stock.make': 'Marke',
  'stock.model': 'Modell',
  'stock.trimOptional': 'Ausstattung (optional)',
  'stock.costOptional': 'Einkaufspreis (optional)',
  'stock.costNote':
    'Ein Preis bringt das Fahrzeug in die Bilanz. Leer lassen, wenn er noch nicht bekannt ist — das wird als unbekannt erfasst, nicht als null.',
  'stock.floorplanned': 'Ein Kreditgeber finanziert dieses Fahrzeug (Floorplan)',
  'stock.moveTitle': 'Wohin es als Nächstes geht',
  'stock.moveNote': 'Notiz (bleibt im Vorgang)',
  'stock.moveTo': 'Auf {to} setzen',
  'stock.soldNote': 'Dieses Fahrzeug ist verkauft. Zum Rückgängigmachen den Vorgang stornieren.',
  'stock.noMovesNote': 'Dieses Fahrzeug kann von hier aus nicht bewegt werden.',

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

  'enum.contactKind.Email': 'E-Mail',
  'enum.contactKind.Phone': 'Telefon',
  'enum.contactKind.Mobile': 'Mobil',

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
  'customers.cameFrom': 'Stammt aus',
  'customers.waysToReach': 'So ist er erreichbar',
  'customers.primary': 'Haupt',
  'customers.address': 'Adresse',
  'customers.noAddress': 'Keine Adresse erfasst.',
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

  'periods.title': 'Die Bücher',
  'periods.loading': 'Die Bücher werden geladen…',
  'periods.denied': 'Sie haben keinen Zugriff auf die Buchhaltung.',
  'periods.failed': 'Die Bücher konnten nicht gelesen werden.',
  'periods.actionFailed': 'Das hat nicht funktioniert.',
  'periods.lede':
    'In einen Monat kann nichts gebucht werden, solange seine Bücher nicht eröffnet sind, und in einen abgeschlossenen Monat auch nicht. Der Abschluss ist etwas, das Sie vornehmen, wenn die Monatsabschlussarbeiten fertig sind — es gibt kein Datum, das ihn für Sie erledigt.',
  'periods.none':
    'Es ist noch kein Monat eröffnet. Bevor Sie einen eröffnen, kann nichts gebucht werden.',

  'periods.openAMonth': 'Monat eröffnen',
  'periods.openLede':
    'Solange ein Monat nicht eröffnet ist, kann nichts mit diesem Datum gebucht werden — ein Verkauf oder eine Werkstattrechnung wird abgelehnt. Die Eröffnung erfolgt bewusst, damit die Bücher einen Anfang haben, den Sie gewählt haben, und nicht einen, der aus der ersten Eingabe abgeleitet wurde.',
  'periods.year': 'Jahr',
  'periods.month': 'Monat',
  'periods.openIt': 'Eröffnen',

  'periods.reopenTitle': '{month} wieder öffnen?',
  'periods.reopenLede':
    'Dieser Monat ist abgeschlossen, und seine Zahlen wurden möglicherweise bereits gemeldet. Das Wiederöffnen wird mit Ihrer Begründung beim Monat festgehalten, damit später jeder nachvollziehen kann, was geschehen ist und warum.',
  'periods.reopenWhy': 'Warum wird er wieder geöffnet?',
  'periods.reopenPlaceholder': 'Eine Lieferantenrechnung kam am 4.',
  'periods.reopenIt': 'Wieder öffnen',
  'periods.leaveClosed': 'Abgeschlossen lassen',
  'periods.reopen': 'Wieder öffnen',

  'periods.closeIt': 'Abschließen',
  'periods.confirmClose':
    '{month} abschließen? Es kann nichts mehr hineingebucht werden, bis er wieder geöffnet wird.',

  'periods.caption': 'Alle Monate der Bücher, neueste zuerst.',
  'periods.colMonth': 'Monat',
  'periods.colCutoff': 'Stichtag',
  'periods.colEntries': 'Buchungen',
  'periods.colState': 'Zustand',

  'periods.historyTitle': 'Was mit den Büchern geschehen ist',
  'periods.wasOpened': '{month} eröffnet',
  'periods.wasClosed': '{month} abgeschlossen',
  'periods.wasReopened': '{month} wieder geöffnet',

  'leads.title': 'Anfragen',
  'leads.show': 'Anzeigen',
  'leads.stillChasing': 'Noch in Bearbeitung',
  'leads.everything': 'Alle',
  'leads.onlyMine': 'Nur meine',
  'leads.take': 'Anfrage aufnehmen',
  'leads.untouchedTitle': 'Darum kümmert sich niemand',
  'leads.untouchedNote': {
    one: 'Bei {count} Anfrage steht kein Name.',
    other: 'Bei {count} Anfragen steht kein Name.',
  },
  'leads.noParticularCar': 'kein bestimmtes Fahrzeug',
  'leads.waitingDays': {
    one: 'wartet seit {count} Tag',
    other: 'wartet seit {count} Tagen',
  },
  'leads.loading': 'Anfragen werden geladen…',
  'leads.denied':
    'Sie haben keinen Zugriff auf die Anfragen dieses Standorts. Fragen Sie eine Führungskraft, falls das nicht stimmen sollte.',
  'leads.failed': 'Die Anfragen konnten nicht geladen werden.',
  'leads.openFailed': 'Diese Anfrage konnte nicht geöffnet werden.',
  'leads.empty':
    'Hier gibt es keine Anfragen. Eine entsteht, sobald jemand anruft oder auf den Hof kommt.',

  'leads.colCustomer': 'Kunde',
  'leads.colAskedAbout': 'Gefragt nach',
  'leads.colCameFrom': 'Herkunft',
  'leads.colDays': 'Tage',
  'leads.colChasedBy': 'Betreut von',
  'leads.colStage': 'Stand',
  'leads.nothingSpecific': 'Nichts Bestimmtes',
  'leads.nobodyYet': 'Noch niemand',
  'leads.you': 'Sie',
  'leads.somebodyElse': 'Jemand anderes',
  'leads.count': { one: '{count} Anfrage', other: '{count} Anfragen' },
  'paging.showingRange': '{first}–{last} von {total} angezeigt.',
  'paging.previous': 'Zurück',
  'paging.next': 'Weiter',

  'leads.cameIn': 'eingegangen am {date}',
  'leads.unclaimed': 'Das hat noch niemand übernommen.',
  'leads.yoursToChase': 'Sie betreuen diese Anfrage.',
  'leads.theirsToChase': '{name} betreut diese Anfrage.',
  'leads.putBack': 'Zurück in den Pool geben',
  'leads.iWillChase': 'Ich übernehme das',
  'leads.takeItOver': 'Übernehmen',
  'leads.handTo': 'Übergeben an',
  'leads.chooseColleague': 'Kollegin oder Kollegen wählen',
  'leads.buildTheDeal': 'Verkauf anlegen',
  'leads.whatHappened': 'Verlauf',
  'leads.finished':
    'Diese Anfrage ist abgeschlossen. Meldet sich der Kunde später erneut, beginnt eine neue.',
  'leads.note': 'Notiz (kommt in den Verlauf)',
  'leads.notePlaceholder': 'Nachricht hinterlassen · kommt Samstag · anderswo gekauft',
  'leads.reopenedHere':
    'Eine verlorene Anfrage, die zurückkommt, wird hier wieder geöffnet statt neu erfasst — so bleibt der erste Anlauf Teil der Geschichte.',

  'leads.moveReopen': 'Wieder öffnen',
  'leads.moveStartChasing': 'Bearbeitung beginnen',
  'leads.moveAppointment': 'Kunde kommt vorbei',
  'leads.moveWon': 'Kunde kauft',
  'leads.moveLost': 'Als verloren markieren',

  'leads.captureTitle': 'Anfrage aufnehmen',
  'leads.findCustomer': 'Kunden suchen',
  'leads.whoIsAsking': 'Wer fragt',
  'leads.chooseSomebody': 'Jemanden wählen…',
  'leads.searchAboveNote':
    'Suchen Sie oben nach der Person. Eine Anfrage muss jemandem gehören — ist die Person neu, legen Sie sie zuerst auf der Kundenseite an.',
  'leads.notOnFile': 'Nicht erfasst? Hier anlegen.',
  'leads.newCustomerTitle': 'Jemand Neues',
  'leads.addAndUse': 'Anlegen und verwenden',
  'leads.whichLocation': 'Welcher Standort',
  'leads.chooseLocation': 'Standort wählen…',
  'leads.onlyLocation':
    'Diese Anfrage gehört zu {name} ({code}), dem einzigen Standort, an dem Sie arbeiten.',
  'leads.howTheyReachedUs': 'Wie der Kunde uns erreicht hat',
  'leads.carAskedAbout': 'Gefragtes Fahrzeug (optional)',
  'leads.whatTheySaid': 'Was der Kunde gesagt hat',
  'leads.whatTheySaidPlaceholder': 'Budget, Inzahlungnahme, bis wann benötigt…',
  'leads.save': 'Anfrage speichern',
  'leads.locationsFailed': 'Ihre Standorte konnten nicht geladen werden.',
  'leads.lookupFailed': 'Das konnte nicht nachgeschlagen werden.',
  'leads.saveFailed': 'Diese Anfrage konnte nicht gespeichert werden.',

  'deals.title': 'Verkäufe',
  'deals.show': 'Anzeigen',
  'deals.stillWorked': 'In Bearbeitung',
  'deals.everything': 'Alle',
  'deals.start': 'Verkauf anlegen',
  'deals.loading': 'Verkäufe werden geladen…',
  'deals.denied':
    'Sie haben keinen Zugriff auf die Verkäufe dieses Standorts. Fragen Sie eine Führungskraft, falls das nicht stimmen sollte.',
  'deals.failed': 'Die Verkäufe konnten nicht geladen werden.',
  'deals.openFailed': 'Dieser Verkauf konnte nicht geöffnet werden.',
  'deals.empty':
    'Hier gibt es keine Verkäufe. Einer entsteht, sobald ein Fahrzeug für jemanden kalkuliert wird.',
  'deals.documentFailed': 'Das Dokument konnte nicht geöffnet werden.',

  'deals.colCustomer': 'Kunde',
  'deals.colVehicle': 'Fahrzeug',
  'deals.colStock': 'Bestandsnr.',
  'deals.colDue': 'Offen',
  'deals.colStage': 'Stand',
  'deals.count': { one: '{count} Verkauf', other: '{count} Verkäufe' },

  'deals.printOrder': 'Bestellung drucken',
  'deals.stockLine': 'Bestandsnr. {stock}',
  'deals.numbersCaption': 'Die Kalkulation dieses Verkaufs',
  'deals.colLine': 'Position',
  'deals.colDescription': 'Bezeichnung',
  'deals.colAmount': 'Betrag',
  'deals.lineProduct': 'Produkt',
  'deals.lineTradeIn': 'Inzahlungnahme',
  'deals.owesMore': 'schuldet mehr, als das Fahrzeug wert ist',
  'deals.dueFromCustomer': 'Vom Kunden zu zahlen',
  'deals.frozen':
    'Die Kalkulation ist eingefroren. Sie war ab dem Einreichen nicht mehr änderbar, damit die Führungskraft genau das genehmigt, was ihr vorgelegt wurde.',
  'deals.whatHappened': 'Verlauf',

  'deals.finished': 'Dieser Verkauf ist abgeschlossen. Es kann nichts mehr damit geschehen.',
  'deals.sendToManager': 'An eine Führungskraft senden',
  'deals.approve': 'Genehmigen',
  'deals.handOver': 'Fahrzeug ausliefern',
  'deals.markLost': 'Als verloren markieren',
  'deals.markedLostNote': 'Am Verkaufstisch als verloren markiert.',
  'deals.cannotApproveOwn':
    'Wer diesen Verkauf aufgebaut hat, darf ihn nicht selbst genehmigen. Sind das Sie, muss eine Führungskraft es tun.',

  'deals.soldWithTheCar': 'Mit dem Fahrzeug verkauft',
  'deals.colProduct': 'Produkt',
  'deals.colPrice': 'Preis',
  'deals.colGross': 'Rohertrag',
  'deals.productGross': '{amount} Rohertrag aus dem, was mit dem Fahrzeug verkauft wurde.',

  'terms.title': 'Die Kalkulation',
  'terms.caption': 'Die Positionen dieses Verkaufs',
  'terms.colLine': 'Position',
  'terms.colDescription': 'Bezeichnung',
  'terms.colAmount': 'Betrag',
  'terms.remove': 'Entfernen',
  'terms.lineKind': 'Art der Position {n}',
  'terms.lineDescription': 'Bezeichnung der Position {n}',
  'terms.lineAmount': 'Betrag der Position {n}',
  'terms.addLine': 'Position hinzufügen',
  'terms.addTradeIn': 'Inzahlungnahme hinzufügen',
  'terms.dropTradeIn': 'Doch keine Inzahlungnahme',
  'terms.tradeInTitle': 'Die Inzahlungnahme',
  'terms.whatTheyTrade': 'Was in Zahlung gegeben wird',
  'terms.whatWeAllow': 'Angerechneter Wert',
  'terms.whatIsOwed': 'Darauf noch offene Restschuld',
  'terms.negativeEquity':
    'Die Restschuld übersteigt den angerechneten Wert, die Differenz wird diesem Verkauf zugeschlagen.',
  'terms.save': 'Kalkulation speichern',
  'terms.rejected': 'Diese Kalkulation wurde nicht akzeptiert.',
  'terms.needsPrice':
    'Jeder Verkauf braucht einen Preis für das Fahrzeug selbst, bevor er gespeichert werden kann.',

  'products.title': 'Mit dem Fahrzeug verkauft',
  'products.loading': 'Verkaufbare Produkte werden geladen…',
  'products.none':
    'Es sind keine Produkte zum Verkauf eingerichtet. Eine Führungskraft legt sie im F&I-Katalog an.',
  'products.lede':
    'Die Preise kommen aus dem Katalog und dürfen geändert werden — was Sie hier eintragen, wird auf diesem Verkauf festgehalten, und spätere Preislistenänderungen rühren es nicht an.',
  'products.colSell': 'Verkaufen',
  'products.colProduct': 'Produkt',
  'products.colPrice': 'Preis',
  'products.colCost': 'Kosten',
  'products.colGross': 'Rohertrag',
  'products.sellThis': '{product} verkaufen',
  'products.priceFor': 'Preis für {product}',
  'products.costOf': 'Kosten von {product}',
  'products.termMonths': { one: '{count} Monat', other: '{count} Monate' },
  'products.withdrawn': 'wird nicht mehr angeboten',
  'products.nothingSelected': 'Nichts ausgewählt.',
  'products.addedToDeal': '{added} zum Verkauf hinzugefügt, macht {gross} Rohertrag.',
  'products.saveFailed': 'Das wurde nicht gespeichert.',
  'products.saveWhatIsSold': 'Verkaufte Produkte speichern',

  'tax.title': 'Steuern',
  'tax.lede':
    'Das wird noch nicht für Sie berechnet — tragen Sie ein, was zutrifft. Jede Zeile hält fest, dass eine Person sie eingegeben hat; genau das müssen Geschäftsleitung und Prüfer später sehen.',
  'tax.workedOutFrom': 'Adresse, nach der die Steuer berechnet wird',
  'tax.state': 'Bundesland oder Region',
  'tax.county': 'Landkreis',
  'tax.postalCode': 'Postleitzahl',
  'tax.country': 'Land',
  'tax.colDescription': 'Steuer',
  'tax.colJurisdiction': 'Steuergebiet',
  'tax.colBasis': 'Bemessungsgrundlage',
  'tax.colRate': 'Satz %',
  'tax.colAmount': 'Betrag',
  'tax.colSource': 'Herkunft',
  'tax.descriptionOfLine': 'Steuer in Zeile {line}',
  'tax.jurisdictionOfLine': 'Steuergebiet in Zeile {line}',
  'tax.basisOfLine': 'Bemessungsgrundlage in Zeile {line}',
  'tax.rateOfLine': 'Satz in Zeile {line}, in Prozent',
  'tax.amountOfLine': 'Berechnete Steuer in Zeile {line}',
  'tax.none': 'Für diesen Vorgang ist noch keine Steuer erfasst.',
  'tax.totalIs': 'Steuer für diesen Vorgang: {total}.',
  'tax.totalLabel': 'Steuer',
  'tax.addLine': 'Steuerzeile hinzufügen',
  'tax.save': 'Steuer speichern',
  'tax.from.EnteredByPerson': 'von einer Person eingegeben',
  'tax.from.Pack': 'eine Steuersatztabelle',
  'tax.from.Vendor': 'ein Steuerdienstleister',
  'tax.fromPack': '{pack} v{version}',

  'startDeal.title': 'Verkauf anlegen',
  'startDeal.findBuyer': 'Käufer suchen',
  'startDeal.buyer': 'Wer kauft',
  'startDeal.chooseBuyer': 'Jemanden wählen…',
  'startDeal.searchAbove':
    'Suchen Sie oben nach der Person. Ist sie neu, legen Sie sie auf der Kundenseite an.',
  'startDeal.fromEnquiry': 'Aus der Anfrage von {name}. Der Verkauf wird damit verknüpft.',
  'startDeal.thatCustomer': 'diesem Kunden',
  'startDeal.whichCar': 'Welches Fahrzeug',
  'startDeal.chooseCar': 'Fahrzeug wählen…',
  'startDeal.nothingAvailable':
    'Derzeit ist nichts auf dem Hof verfügbar. Ein Fahrzeug, das bereits zu einem anderen Verkauf gehört, ist bis zu dessen Ende reserviert.',
  'startDeal.chooseCarFirst': 'Wählen Sie zuerst ein Fahrzeug.',
  'startDeal.submit': 'Verkauf anlegen',
  'startDeal.starting': 'Wird angelegt…',
  'startDeal.failed': 'Dieser Verkauf konnte nicht angelegt werden.',
  'startDeal.stockFailed': 'Die Bestandsliste konnte nicht geladen werden.',
  'startDeal.customerFailed': 'Dieser Kundendatensatz konnte nicht gelesen werden.',

  'staff.title': 'Mitarbeiter',
  'staff.loading': 'Die Belegschaft wird geladen…',
  'staff.denied':
    'Sie haben keinen Zugriff auf die Mitarbeiterliste. Fragen Sie eine Führungskraft, falls Sie sie brauchen.',
  'staff.failed': 'Die Mitarbeiterliste konnte nicht gelesen werden.',
  'staff.actionFailed': 'Das hat nicht funktioniert.',
  'staff.empty': 'Hier ist noch niemand.',
  'staff.add': 'Jemanden anlegen',
  'staff.caption': 'Alle, deren Zugriff einen Standort erreicht, an dem Sie arbeiten.',
  'staff.colName': 'Name',
  'staff.colEmail': 'E-Mail',
  'staff.colHolds': 'Rechte',
  'staff.colSecondFactor': 'Zweiter Faktor',
  'staff.colState': 'Zustand',
  'staff.holdsNothing': 'Noch keine',

  'staff.stateStopped': 'Gesperrt',
  'staff.stateAwaiting': 'Wartet auf erstes Passwort',
  'staff.stateWorking': 'Aktiv',

  'staff.codeFor': 'Code für {name}',
  'staff.readItOut':
    'Lesen Sie ihn der Person vor. Damit legt sie auf dem Anmeldebildschirm ihr eigenes Passwort fest — niemand sonst tippt es jemals ein, Sie eingeschlossen.',
  'staff.onlyTimeShown': 'Dies ist das einzige Mal, dass er angezeigt werden kann.',
  'staff.onlyTimeShownRest':
    'Gespeichert wird nur sein Hash, er lässt sich also nicht noch einmal nachschlagen. Geht er verloren, erzeugen Sie einen neuen — damit wird dieser ungültig. Er läuft am {expires} ab.',
  'staff.passedItOn': 'Ich habe ihn weitergegeben',

  'staff.addTitle': 'Jemanden anlegen',
  'staff.addLede':
    'Die Person kann sich erst anmelden, wenn sie mit dem hier erzeugten Code ein Passwort festgelegt hat. Sie sehen und wählen ihr Passwort nie.',
  'staff.name': 'Name',
  'staff.email': 'E-Mail',
  'staff.addAndMakeCode': 'Anlegen und Code erzeugen',

  'staff.hasSecondFactor': 'hat einen zweiten Faktor',
  'staff.noSecondFactor': 'kein zweiter Faktor',
  'staff.whatTheyHold': 'Welche Rechte sie hat',
  'staff.holdsNothingYet': 'Noch keine — sie kann sich anmelden und sieht nichts.',
  'staff.everywhere': 'überall',
  'staff.oneLocation': 'ein Standort',
  'staff.takeItAway': 'Entziehen',
  'staff.giveARole': 'Eine Rolle geben',
  'staff.role': 'Rolle',
  'staff.chooseRole': 'Rolle wählen',
  'staff.holdingGrants': 'Diese Rolle gewährt: {permissions}',
  'staff.andObligesSecondFactor': ' — und verpflichtet zur Einrichtung eines zweiten Faktors.',
  'staff.where': 'Wo',
  'staff.everywhereInOrg': 'Überall in der Gruppe',
  'staff.giveThem': 'Zuweisen',
  'staff.makeNewCode': 'Neuen Code erzeugen',
  'staff.stopAccount': 'Konto sperren',
  'staff.letThemBackIn': 'Konto wieder freigeben',
  'staff.stoppingNote':
    'Das Sperren eines Kontos beendet dessen Sitzungen mit der nächsten Anfrage und löscht nichts — der Name muss weiterhin bei der geleisteten Arbeit stehen.',

  'parts.title': 'Teile',
  'parts.loading': 'Teilekatalog wird geladen…',
  'parts.denied':
    'Sie haben keinen Zugriff auf die Teile dieses Standorts. Fragen Sie eine Führungskraft, falls das nicht stimmen sollte.',
  'parts.failed': 'Der Katalog konnte nicht gelesen werden.',
  'parts.actionFailed': 'Das hat nicht funktioniert.',
  'parts.add': 'Teil anlegen',
  'parts.find': 'Teil suchen',
  'parts.findPlaceholder': 'Nummer oder Bezeichnung',
  'parts.findHint':
    'Die Nummer wird unabhängig von der Schreibweise gefunden — MZ-690411, mz690411 und MZ 690 411 führen zum selben Teil.',
  'parts.catalogueEmpty': 'Der Katalog ist noch leer.',
  'parts.noMatches': 'Dazu passt nichts.',
  'parts.colNumber': 'Nummer',
  'parts.colDescription': 'Bezeichnung',
  'parts.colWhere': 'Wo',
  'parts.colOnHand': 'Bestand',
  'parts.colCostEach': 'Kosten je Stück',
  'parts.notStocked': 'Nicht bevorratet',
  'parts.oneLocation': 'ein Standort',
  'parts.noneOnHand': 'Keine',

  'parts.costingTitle': 'Wie Teile bewertet werden',
  'parts.costingMethod': 'Verfahren',
  'parts.costingFutureOnly': 'nur für künftige Verkäufe',
  'parts.costingNote':
    'Bereits berechnete Arbeiten behalten die Kosten, zu denen sie verkauft wurden — eine Änderung hier kann einen bereits gemeldeten Monat nicht rückwirkend ändern.',
  'parts.costingApplies': 'Das gilt {futureOnly}. {rest}',

  'parts.addTitle': 'Teil anlegen',
  'parts.addLede':
    'Eine Teilenummer bezeichnet an jedem Standort dasselbe Bauteil, dies ist also eine Änderung auf Gruppenebene. Der Bestand selbst gehört zu dem Lager, auf das er eingebucht wird.',
  'parts.partNumber': 'Teilenummer',
  'parts.description': 'Bezeichnung',
  'parts.addIt': 'Anlegen',

  'parts.noneVisible': 'Davon liegt nichts in einem für Sie sichtbaren Lager.',
  'parts.shelfHeading': '{code} — {quantity} auf Lager zu je {cost}',
  'parts.deliveriesCaption': 'Zugänge von {part} bei {code}.',
  'parts.colReceived': 'Zugang',
  'parts.colNote': 'Beleg',
  'parts.colCameIn': 'Eingegangen',
  'parts.colLeft': 'Verbleibend',

  'parts.bookIn': 'Lieferung einbuchen',
  'parts.ontoWhichShelf': 'Auf welches Lager',
  'parts.howMany': 'Menge',
  'parts.costEach': 'Kosten je Stück',
  'parts.deliveryNote': 'Lieferschein',
  'parts.bookItIn': 'Einbuchen',

  'dash.soFar': 'Bisher in diesem Monat.',
  'dash.asFinished': 'Der Monat, wie er abgeschlossen wurde.',
  'dash.whichMonth': 'Welcher Monat',
  'dash.previousMonth': 'Vorheriger Monat',
  'dash.nextMonth': 'Nächster Monat',
  'dash.previousMonthTitle': 'Vorheriger Monat ( [ )',
  'dash.nextMonthTitle': 'Nächster Monat ( ] )',
  'dash.rooftop': 'Standort',
  'dash.everywhere': 'Alle für mich sichtbaren',
  'dash.loading': 'Der Monat wird zusammengerechnet…',
  'dash.denied': 'Sie haben auf keine der Zahlen dieser Übersicht Zugriff.',
  'dash.failed': 'Der Monat konnte nicht geladen werden.',

  'dash.withheldTrading':
    'Die Beträge dieses Monats sind für Sie nicht einsehbar, die Zahlen unten betreffen daher nur den Bestand.',
  'dash.withheldStock':
    'Der Bestand ist für Sie nicht einsehbar, dieser Monat zeigt daher nur das Verkaufte.',

  'dash.booksOpen': 'Die Bücher sind offen, diese Zahlen können sich also noch ändern.',
  'dash.booksClosed': 'Die Bücher sind abgeschlossen. Dies sind die gemeldeten Zahlen.',
  'dash.booksClosedOn':
    'Die Bücher wurden am {date} abgeschlossen. Dies sind die gemeldeten Zahlen.',
  'dash.booksNotOpened':
    'Für diesen Monat hat niemand die Bücher eröffnet, es kann also nichts hineingebucht werden.',
  'dash.booksUnknown': 'Ob die Bücher offen sind, ist für Sie nicht einsehbar.',

  'dash.whatTheMonthMade': 'Was der Monat gebracht hat',
  'dash.totalGross': 'Rohertrag gesamt',
  'dash.financeShort': 'F&I',
  'dash.whatSold': 'Was verkauft wurde',
  'dash.carsDelivered': 'Ausgelieferte Fahrzeuge',
  'dash.jobsInvoiced': 'Berechnete Aufträge',
  'dash.grossPerCar': 'Rohertrag je Fahrzeug',
  'dash.frontAndBack': 'Fahrzeug und F&I zusammen',

  'dash.whereGrossCameFrom': 'Woher der Rohertrag kam',
  'dash.colDepartment': 'Abteilung',
  'dash.colRevenue': 'Umsatz',
  'dash.colCost': 'Kosten',
  'dash.colGross': 'Rohertrag',
  'dash.colMargin': 'Marge',
  'dash.total': 'Summe',

  'dash.howOldTheStockIs': 'Wie alt der Bestand ist',
  'dash.unsoldAsAt': ' — {count} unverkauft, Stand {date}',
  'dash.nothingUnsold': 'Nichts Unverkauftes auf dem Hof.',
  'dash.standingLongest': 'Am längsten stehend',
  'dash.colStock': 'Bestandsnr.',
  'dash.colVehicle': 'Fahrzeug',
  'dash.colStatus': 'Status',
  'dash.colDays': 'Tage',
  'dash.estimatedAge':
    'Es wurde kein Zugangsdatum erfasst, daher zählt dies ab der Erfassung.',
  'dash.estimatedAgeNote':
    '* gezählt ab der Erfassung des Fahrzeugs, da kein Zugangsdatum hinterlegt war.',

  'workshop.title': 'Werkstatt',
  'workshop.loading': 'Die Werkstatt wird geladen…',
  'workshop.denied':
    'Sie haben keinen Zugriff auf die Werkstatt dieses Standorts. Fragen Sie eine Führungskraft, falls das nicht stimmen sollte.',
  'workshop.failed': 'Die Werkstattliste konnte nicht gelesen werden.',
  'workshop.actionFailed': 'Das hat nicht funktioniert.',
  'workshop.openOnly': 'Nur noch offene Aufträge',
  'workshop.nothingOpen': 'Derzeit ist nichts in der Werkstatt.',
  'workshop.empty': 'Hier gibt es noch keine Aufträge.',
  'workshop.caption': 'Werkstattaufträge an den Standorten, die Sie betreuen.',
  'workshop.colJob': 'Auftrag',
  'workshop.colCustomer': 'Kunde',
  'workshop.colVehicle': 'Fahrzeug',
  'workshop.colCameInFor': 'Grund',
  'workshop.colWaiting': 'Wartet',
  'workshop.colDue': 'Offen',
  'workshop.colStage': 'Stand',

  'workshop.waitingTitle': 'Wartet auf den Kunden',
  'workshop.waitingNote': {
    one: 'Ein Auftrag enthält Arbeiten, denen noch niemand zugestimmt hat. Er kann nicht berechnet werden, bevor jemand anruft.',
    other:
      '{count} Aufträge enthalten Arbeiten, denen noch niemand zugestimmt hat. Keiner davon kann berechnet werden, bevor jemand anruft.',
  },
  'workshop.toAskAbout': {
    one: '{count} Position zum Nachfragen',
    other: '{count} Positionen zum Nachfragen',
  },
  'workshop.pendingNote': {
    one: 'Eine Arbeit wartet auf den Kunden. Sie kann nicht berechnet werden, bevor er antwortet.',
    other:
      '{count} Arbeiten warten auf den Kunden. Keine davon kann berechnet werden, bevor er antwortet.',
  },

  'workshop.stageBooked': 'Terminiert',
  'workshop.stageInProgress': 'In Arbeit',
  'workshop.stageCompleted': 'Arbeiten fertig',
  'workshop.stageInvoiced': 'Berechnet',
  'workshop.stageCancelled': 'Storniert',

  'workshop.moveInProgress': 'Arbeit beginnen',
  'workshop.moveCompleted': 'Arbeiten sind fertig',
  'workshop.moveInvoiced': 'Berechnen',
  'workshop.moveCancelled': 'Auftrag stornieren',
  'workshop.moveBooked': 'Zurück auf terminiert',

  'workshop.miles': '{count} Meilen',
  'workshop.bookedIn': 'angenommen am {date}',
  'workshop.printJobSheet': 'Auftragszettel drucken',
  'workshop.printInvoice': 'Rechnung drucken',
  'workshop.whatHappened': 'Verlauf',

  'workshop.theWork': 'Die Arbeiten',
  'workshop.nothingWrittenUp': 'Noch nichts erfasst.',
  'workshop.colWhat': 'Art',
  'workshop.colDetail': 'Detail',
  'workshop.colAgreed': 'Freigegeben?',
  'workshop.colAmount': 'Betrag',
  'workshop.nobodyAsked': 'Noch nicht gefragt',
  'workshop.saidNo': 'Abgelehnt',
  'workshop.agreed': 'Freigegeben',
  'workshop.iRangThem': 'Ich habe angerufen',
  'workshop.notNow': 'Jetzt nicht',
  'workshop.howObtained': 'Wie die Freigabe eingeholt wurde',
  'workshop.howObtainedPlaceholder': 'Angerufen 10:40, mit Frau Okafor gesprochen',
  'workshop.theySaidYes': 'Kunde stimmt zu',
  'workshop.theySaidNo': 'Kunde lehnt ab',
  'workshop.hoursAtRate': '{hours} Std. zu {rate}',

  'workshop.writeUpMore': 'Weitere Arbeiten erfassen',
  'workshop.lineKind': 'Art',
  'workshop.lineDescription': 'Bezeichnung',
  'workshop.lineJob': 'Arbeit aus dem Katalog',
  'workshop.lineJobNote':
    'Optional. Bei Auswahl werden Vorgabezeit und der Satz dieses Standorts eingesetzt; Ihre Eingabe hat Vorrang.',
  'workshop.setupLink': 'Werkstatt einrichten',
  'workshop.clockTitle': 'Zeit auf diesem Auftrag',
  'workshop.clockedSoFar': 'Bisher {hours} Stunden gestempelt. Ein Techniker, der noch am Auftrag ist, zählt erst beim Ausstempeln.',
  'workshop.clockWho': 'Jemanden einstempeln…',
  'workshop.clockOn': 'Einstempeln',
  'workshop.clockOff': 'Ausstempeln',
  'workshop.onSince': 'dabei seit {since}',
  'workshop.someone': 'Jemand hier',
  'labour.hoursClocked': 'Gestempelte Stunden',
  'labour.productivity': 'Produktivität',
  'labour.colClocked': 'Gestempelt',
  'labour.colProductivity': 'Produktivität',
  'labour.notClocked': 'Nicht gestempelt',

  'serviceSetup.title': 'Werkstatt-Einrichtung',
  'serviceSetup.loading': 'Einrichtung wird geladen…',
  'serviceSetup.denied': 'Sie können die Werkstatt nicht sehen.',
  'serviceSetup.backToWorkshop': 'Zurück zur Werkstatt',
  'serviceSetup.ratesTitle': 'Was eine Stunde kostet',
  'serviceSetup.ratesNote':
    'Pro Standort festgelegt. Zwei Standorte müssen nicht dasselbe berechnen, und Garantie wird zum Satz des Herstellers erstattet.',
  'serviceSetup.location': 'Standort',
  'serviceSetup.chooseLocation': 'Standort wählen…',
  'serviceSetup.perHour': 'Pro Stunde',
  'serviceSetup.notSet': 'Nicht festgelegt',
  'serviceSetup.setRate': 'Satz festlegen',
  'serviceSetup.jobsTitle': 'Arbeiten, die die Werkstatt verkauft',
  'serviceSetup.jobsNote':
    'Für alle Standorte gemeinsam, wie eine Teilenummer, damit dieselbe Arbeit überall dasselbe bedeutet.',
  'serviceSetup.code': 'Code',
  'serviceSetup.describes': 'Beschreibt',
  'serviceSetup.standardHours': 'Vorgabezeit',
  'serviceSetup.whoPays': 'Wer normalerweise zahlt',
  'serviceSetup.withdraw': 'Zurückziehen',
  'serviceSetup.restore': 'Wiederherstellen',
  'serviceSetup.addJob': 'Arbeit hinzufügen',
  'serviceSetup.frozenNote':
    'Hier wird nie etwas gelöscht. Zurückziehen bietet eine Arbeit nicht mehr an und lässt jeden Auftrag, der sie bereits nennt, korrekt lesbar.',
  'workshop.hours': 'Stunden',
  'workshop.rate': 'Stundensatz',
  'workshop.amount': 'Betrag',
  'workshop.fromTheShelf': 'Aus dem Lager',
  'workshop.notFromStock': 'Nicht aus dem Lager (unten eintragen)',
  'workshop.howMany': 'Wie viele',
  'workshop.onTheShelf': {
    one: '{number}: {count} am Lager.',
    other: '{number}: {count} am Lager.',
  },
  'workshop.addLine': 'Hinzufügen',

  'workshop.totalsCaption': 'Was der Auftrag ergibt.',
  'workshop.totalLabour': 'Arbeitslohn',
  'workshop.totalParts': 'Teile',
  'workshop.totalSublet': 'Fremdleistung',
  'workshop.totalDue': 'Offen',

  'workshop.whoIsOnIt': 'Wer daran arbeitet',
  'workshop.nobodyYet': 'Noch niemand',
  'workshop.invoicedNothingMore': 'Berechnet am {date}. Die Arbeit ist fertig; was offen ist, steht unten.',
  'workshop.jobFinished': 'Dieser Auftrag ist abgeschlossen.',
  'workshop.assignedElsewhere':
    'Jemandem zugewiesen, der nicht auf Ihrer Mitarbeiterliste steht — die Person arbeitet möglicherweise an einem anderen Standort.',
  'workshop.removeLine': 'Entfernen',
  'workshop.moveNote': 'Notiz (kommt in die Akte)',
  'workshop.howObtainedHint':
    'Darauf kommt es an, falls die Rechnung je bestritten wird. Halten Sie fest, mit wem Sie wann gesprochen haben.',
  'workshop.toAsk': {
    one: '{count} nachzufragen',
    other: '{count} nachzufragen',
  },
  'workshop.labourReport': 'Arbeitsbericht',

  'workshop.colWhoPays': 'Wer zahlt',
  'workshop.linePayType': 'Wer zahlt',
  'workshop.notCustomersCall': 'Nicht Sache des Kunden',
  'workshop.totalWarranty': 'Garantie',
  'workshop.totalInternal': 'Intern',
  'workshop.totalWork': 'Gesamte Arbeit',
  'workshop.writeUpNote':
    'Alles, was jetzt hinzukommt, braucht die Antwort des Kunden, bevor es berechnet werden darf — genau darum geht es. Schreiben Sie es auf, solange Sie es vor sich haben.',
  'workshop.writeUpNoteOther':
    'Dafür muss niemand beim Kunden anrufen, denn er zahlt es nicht. Es steht trotzdem im Auftrag, damit die Arbeit erfasst und die Stunden gezählt werden.',

  // --- Was die Werkstatt verkauft hat ---------------------------------------
  'labour.title': 'Arbeitsleistung',
  'labour.from': 'Von',
  'labour.to': 'Bis',
  'labour.backToWorkshop': 'Zurück zur Werkstatt',
  'labour.loading': 'Arbeitszahlen werden berechnet…',
  'labour.denied':
    'Sie haben keinen Zugriff auf die Zahlen dieser Werkstatt. Fragen Sie eine Führungskraft, wenn Ihnen das falsch vorkommt.',

  'labour.headlineCaption':
    'Verkaufte Stunden, Umsatz aus Arbeitsleistung und was eine Stunde tatsächlich erbracht hat.',
  'labour.hoursSold': 'Verkaufte Stunden',
  'labour.revenue': 'Umsatz Arbeitsleistung',
  'labour.effectiveRate': 'Was eine Stunde erbracht hat',

  'labour.byTechnician': 'Nach Mechaniker',
  'labour.technicianCaption':
    'Stunden und Umsatz je Mechaniker über den Zeitraum.',
  'labour.colWho': 'Mechaniker',
  'labour.colHours': 'Stunden',
  'labour.colRevenue': 'Umsatz',
  'labour.colRate': 'Pro Stunde',
  'labour.nobodyCredited': 'Niemandem zugerechnet',
  'labour.notNamed': 'Ohne Namen',
  'labour.namesUnavailable':
    'Die Mechaniker werden hier nicht namentlich genannt, weil Sie die Mitarbeiterliste nicht lesen dürfen. Stunden und Beträge stimmen trotzdem.',
  'labour.nothingInvoiced':
    'In diesem Zeitraum wurde nichts berechnet, es gibt also keine Stunden zu berichten.',

  'labour.byPayer': 'Wer gezahlt hat',
  'labour.payerCaption':
    'Stunden und Umsatz, aufgeteilt danach, wer die Arbeit begleicht.',
  'labour.colPayer': 'Bezahlt von',

  'labour.notMeasuredTitle': 'Was hier nicht gemessen wird',
  'labour.notMeasuredWhy':
    'An diesen beiden Zahlen wird eine Werkstatt üblicherweise gemessen, und keine von beiden lässt sich aus dem, was dieses System erfasst, ehrlich berechnen. Beide brauchen etwas, das hier nie eingegeben wurde.',
  'labour.noEfficiency':
    'Effizienz — geleistete Stunden im Verhältnis zu verfügbaren Stunden. Es gibt keinen Dienstplan, also nichts, wodurch geteilt werden könnte.',
  'labour.noProductivity':
    'Produktivität — berechnete Stunden im Verhältnis zu gestempelten Stunden. Es gibt keine Stempeluhr, also nichts, wodurch geteilt werden könnte.',
  'labour.period':
    'Gezählt aus Arbeiten, die zwischen {from} und {to} berechnet wurden. Laufende Arbeit ist kein Umsatz.',

  // --- Sicherheitsrückrufe ---------------------------------------------------
  'recalls.title': 'Sicherheitsrückrufe',
  'recalls.onRequest':
    'Dies fragt die Verkehrssicherheitsbehörde, läuft also nur, wenn Sie es anstoßen.',
  'recalls.check': 'Nach Rückrufen suchen',
  'recalls.checkAgain': 'Erneut suchen',
  'recalls.checking': 'Behörde wird gefragt…',
  'recalls.caveat':
    'Dies sind die Kampagnen, die für einen {make} {model} aus {year} veröffentlicht sind. Das Verzeichnis wird nach Modell geführt, nicht nach Fahrzeug: Es sagt nicht, ob bei diesem Wagen die Arbeit erledigt wurde — das weiß nur der Hersteller.',
  'recalls.noneFound':
    'Für dieses Modell ist keine Kampagne veröffentlicht. Das ist nicht dasselbe, wie dieses Fahrzeug geprüft zu haben.',
  'recalls.doNotDrive': 'Nicht fahren',
  'recalls.parkOutside': 'Im Freien abstellen',
  'recalls.remedy': 'Abhilfe: {remedy}',

  // --- Passkeys --------------------------------------------------------------
  'passkey.useOne': 'Passkey verwenden',
  'passkey.needDealerGroup':
    'Geben Sie zuerst Ihre Händlergruppe ein — sie entscheidet, bei welchem Autohaus Sie sich anmelden.',
  'passkey.ceremonyFailed':
    'Ihr Gerät konnte das nicht abschließen. Versuchen Sie es erneut oder melden Sie sich mit Ihrem Passwort an.',

  'passkey.title': 'Passkeys',
  'passkey.lede':
    'Ein Passkey meldet Sie mit dem Telefon oder Rechner an, den Sie ohnehin entsperren, statt mit einem Passwort. Ihr Passwort gilt weiterhin, und nichts auf diesem Bildschirm nimmt es Ihnen weg.',
  'passkey.addTitle': 'Passkey hinzufügen',
  'passkey.addNote':
    'Ihr Gerät bittet Sie um Bestätigung. Nichts Geheimes verlässt es — nur ein öffentlicher Schlüssel, mit dem niemand etwas anfangen kann, der ihn kopiert.',
  'passkey.label': 'Wie soll er heißen',
  'passkey.labelPlaceholder': 'Arbeitsrechner',
  'passkey.labelHint':
    'Diesen Namen sehen Sie, wenn Sie ihn wieder entfernen: Benennen Sie das Gerät, nicht sich selbst.',
  'passkey.add': 'Hinzufügen',
  'passkey.adding': 'Warten auf Ihr Gerät…',
  'passkey.added': '{label} ist registriert.',
  'passkey.unsupported':
    'Dieser Browser kann keine Passkeys verwenden. Die meisten können es, über eine Verbindung, die nicht einfaches http ist.',

  'passkey.yoursTitle': 'Ihre Passkeys',
  'passkey.loading': 'Ihre Passkeys werden geladen…',
  'passkey.none': 'Sie haben noch keine Passkeys.',
  'passkey.caption': {
    one: '{count} Passkey in diesem Konto',
    other: '{count} Passkeys in diesem Konto',
  },
  'passkey.colLabel': 'Name',
  'passkey.colAdded': 'Hinzugefügt',
  'passkey.colLastUsed': 'Zuletzt verwendet',
  'passkey.neverUsed': 'Nie verwendet',
  'passkey.forget': 'Entfernen',
  'passkey.forgetConfirm':
    '{label} entfernen? Dieses Gerät kann Sie dann nicht mehr anmelden, und das lässt sich nicht rückgängig machen.',
  'passkey.forgot': '{label} ist entfernt.',

  // --- Wieder Zugang zum Konto bekommen ----------------------------------------
  'recover.link': 'Ich habe mein Passwort vergessen',
  'recover.title': 'Wieder hineinkommen',
  'recover.lede':
    'Wählen Sie, womit Sie nachweisen können, dass es Ihr Konto ist. In jedem Fall vergeben Sie hier direkt ein neues Passwort.',
  'recover.withAuthenticator': 'Meine Authenticator-App verwenden',
  'recover.withAuthenticatorHint':
    'Für alle mit Zwei-Schritt-Anmeldung. Ein Code aus der App oder einer der Wiederherstellungscodes, die Sie aufbewahrt haben.',
  'recover.withCode': 'Einen Code von der Leitung verwenden',
  'recover.withCodeHint':
    'Lassen Sie sich im Bildschirm „Personal“ einen ausstellen. Er wird Ihnen vorgelesen und gilt vier Stunden.',
  'recover.email': 'E-Mail',
  'recover.codeFromApp': 'Code aus Ihrer Authenticator-App',
  'recover.codeFromManager': 'Der Code, den Sie erhalten haben',
  'recover.newPassword': 'Neues Passwort',
  'recover.newPasswordAgain': 'Neues Passwort wiederholen',
  'recover.mismatch': 'Die beiden stimmen nicht überein.',
  'recover.submit': 'Passwort festlegen',
  'recover.working': 'Wird gespeichert…',
  'recover.doneTitle': 'Erledigt',
  'recover.doneLede':
    'Ihr Passwort ist geändert, und alle angemeldeten Geräte wurden abgemeldet. Melden Sie sich mit dem neuen an.',
  'recover.toSignIn': 'Zur Anmeldung',
  'recover.back': 'Anderen Weg wählen',
  'recover.noMethods':
    'Diese Installation kann ein Konto nicht selbst wiederherstellen. Bitten Sie die Leitung, Sie neu einzurichten.',

  'staff.resetTitle': 'Passwort zurücksetzen',
  'staff.reset': 'Zurücksetzcode ausstellen',
  'staff.resetting': 'Wird ausgestellt…',
  'staff.resetNote':
    'Damit kann sich die Person wieder als sie selbst anmelden. Lesen Sie den Code vor — er wird einmal angezeigt und gilt vier Stunden.',
  'staff.resetWarning':
    'Sie geben die Möglichkeit weiter, sich als diese Person anzumelden. Vergewissern Sie sich, dass Sie wirklich mit ihr sprechen.',
  'staff.resetIssued': 'Am {when} wurde ein Zurücksetzcode ausgestellt und noch nicht verwendet.',
  'staff.resetDone': 'Ich habe ihn vorgelesen',

  // The signal band on the deal desk: approvals somebody is blocking.
  'deals.awaitingTitle': 'Wartet auf eine Freigabe',
  'deals.awaitingNote': {
    one: '{count} Verkauf ist von niemandem freigegeben und kann bis dahin nicht ausgeliefert werden.',
    other: '{count} Verkäufe sind von niemandem freigegeben und können bis dahin nicht ausgeliefert werden.',
  },
  // Changing a value where it is written. See shared/InlineEdit.tsx.
  'inline.changeThis': '{label}: {value}. Zum Ändern drücken.',
  'inline.saved': 'Gespeichert',

  // --- Das Werkstattbuch ------------------------------------------------------
  // Erwartete Fahrzeuge, die noch nicht da sind. „Termin“, nie „Zeitfenster“:
  // Eine Werkstatt bucht einen Vormittag, keine vierzig Minuten.
  'enum.appointmentStatus.Scheduled': 'Erwartet',
  'enum.appointmentStatus.Arrived': 'Angekommen',
  'enum.appointmentStatus.NoShow': 'Nicht gekommen',
  'enum.appointmentStatus.Cancelled': 'Abgesagt',

  'diary.title': 'Erwartet',
  'diary.loading': 'Werkstattbuch wird geladen…',
  'diary.empty': 'Nichts gebucht. Das Werkstattbuch ist leer.',
  'diary.count': {
    one: '{count} Fahrzeug erwartet',
    other: '{count} Fahrzeuge erwartet',
  },
  'diary.dayLoad': {
    one: '{count} Fahrzeug, {hours} Std. Arbeit',
    other: '{count} Fahrzeuge, {hours} Std. Arbeit',
  },
  'diary.dayLoadSome': {
    one: '{count} Fahrzeug, {hours} Std. gebucht und {unestimated} nicht geschätzt',
    other: '{count} Fahrzeuge, {hours} Std. gebucht und {unestimated} nicht geschätzt',
  },
  'diary.unestimated': 'Nicht geschätzt',
  'diary.colWhen': 'Wann',
  'diary.colCustomer': 'Kunde',
  'diary.colVehicle': 'Fahrzeug',
  'diary.colReason': 'Wofür',
  'diary.colHours': 'Schätz.',
  'diary.colWhat': 'Was nun',
  'diary.itsHere': 'Ist da',
  'diary.arriving': 'Auftrag wird angelegt…',
  'diary.didNotCome': 'Nicht gekommen',
  'diary.becameJob': 'Auftrag {number}',

  'diary.book': 'Fahrzeug einbuchen',
  'diary.bookTitle': 'Fahrzeug einbuchen',
  'diary.customer': 'Kunde',
  'diary.vehicle': 'Fahrzeug',
  'diary.when': 'Wann',
  'diary.hours': 'Erwartete Arbeitsstunden',
  'diary.hoursHint': 'Leer lassen, wenn es noch niemand geschätzt hat.',
  'diary.reason': 'Weshalb das Fahrzeug kommt',
  'diary.reasonPlaceholder': 'Jahresinspektion',
  'diary.take': 'Buchen',
  'diary.taking': 'Wird gebucht…',
  'diary.pickCustomer': 'Kunde wählen',
  'diary.pickVehicle': 'Fahrzeug wählen',
  'diary.pickCustomerFirst': 'Zuerst den Kunden wählen — danach werden seine Fahrzeuge angeboten.',

  // --- Die Verwaltungskonsole -------------------------------------------------
  // Bewusst ein anderes Vokabular als das des Autohauses: Wer diese Bildschirme
  // liest, betreibt die Installation. „Autohaus“ heißt hier ein Konto auf einem
  // Server, nicht ein Ort mit Ausstellungsfläche.
  'admin.badge': 'Verwaltung',
  'admin.navDealerships': 'Autohäuser',
  'admin.navSupportAccess': 'Support-Zugriff',

  'admin.signInLede': 'Damit melden Sie sich an der Installation an, nicht an einem Autohaus.',
  'admin.signInCode': 'Code aus Ihrer Authenticator-App',
  'admin.signInCodeNote': 'Nur leer lassen, wenn Sie noch keine eingerichtet haben.',

  'admin.secondFactorTitle': 'Zweiten Faktor einrichten',
  'admin.secondFactorRequired':
    'Administratorkonten müssen einen haben. Bis Sie ihn einrichten, ist dies der einzige Bildschirm, den Sie nutzen können.',
  'admin.secondFactorIntro':
    'Dieses Konto kann jedes Autohaus dieser Installation betreten — ein Passwort allein genügt nicht, um es zu schützen.',
  'admin.noRecoveryCodes':
    'Für ein Administratorkonto gibt es keine Wiederherstellungscodes. Wenn Sie dieses Telefon verlieren, muss jemand mit Datenbankzugriff den Faktor für Sie zurücksetzen.',

  'admin.dealerships': 'Autohäuser',
  'admin.setUpDealership': 'Autohaus einrichten',
  // "Aussetzen", nicht "Sperren": der Status heißt "Ausgesetzt", und zwei Wörter
  // für einen Zustand lesen sich wie zwei verschiedene Zustände.
  'admin.suspendConfirm':
    '{name} aussetzen? Alle dort werden sofort abgemeldet und können nicht arbeiten, bis der Zugang wieder aufgenommen wird.',
  'admin.dealershipReady': '{name} ist bereit',
  'admin.firstManager':
    'Die erste Leitung dort ist {email}. Lesen Sie ihr diesen Code vor — damit vergibt sie im Anmeldebildschirm ihr eigenes Passwort.',
  'admin.codeShownOnce': 'Nur dieses eine Mal kann er angezeigt werden.',
  'admin.codeShownOnceWhy':
    'Gespeichert wird nur eine verschlüsselte Kopie, er lässt sich also nicht erneut nachschlagen — geht er verloren, kann der Leitung im Bildschirm „Personal“ des Autohauses ein neuer ausgestellt werden. Die Bücher sind offen, es kann sofort gearbeitet werden.',
  'admin.passedItOn': 'Ich habe ihn weitergegeben',
  'admin.loadingDealerships': 'Liste der Autohäuser wird geladen…',
  'admin.noDealerships':
    'Noch keine Autohäuser auf dieser Installation. Richten Sie oben das erste ein.',
  'admin.dealershipsCaption': {
    one: '{count} Autohaus auf dieser Installation',
    other: '{count} Autohäuser auf dieser Installation',
  },
  'admin.colName': 'Name',
  'admin.colKey': 'Schlüssel',
  'admin.colStatus': 'Status',
  'admin.colSchema': 'Schema',
  'admin.colInService': 'In Betrieb',
  'admin.resume': 'Fortsetzen',
  'admin.suspend': 'Aussetzen',

  'admin.setUpNote':
    'Dies legt ihre Datenbank an, eröffnet ihre Bücher für diesen Monat und erstellt eine Leitung, die anschließend alle anderen hinzufügt. Sie werden ihr Passwort nie sehen und nie wählen.',
  'admin.dealershipName': 'Name des Autohauses',
  'admin.shortName': 'Kurzname',
  'admin.shortNameHint':
    'Kleinbuchstaben, Ziffern und Bindestriche. Ihre Mitarbeitenden tippen ihn beim Anmelden ein, und er lässt sich danach nicht mehr ändern.',
  'admin.firstLocation': 'Erster Standort',
  'admin.firstLocationPlaceholder': 'Hauptstandort',
  'admin.locationCode': 'Standortcode',
  'admin.managerName': 'Name der Leitung',
  'admin.managerEmail': 'E-Mail der Leitung',
  'admin.setItUp': 'Einrichten',
  'admin.settingItUp': 'Wird eingerichtet…',
  'admin.notCreated': 'Das Autohaus wurde nicht angelegt.',

  'admin.supportAccess': 'Support-Zugriff',
  'admin.supportEnter': 'Ein Autohaus betreten',
  'admin.supportLede':
    'Sie können ihre Daten lesen und nichts ändern, höchstens eine Stunde lang. Es erscheint in ihrem eigenen Protokoll, mit Ihrem Namen und dem Grund, den Sie hier angeben.',
  'admin.supportDealership': 'Autohaus',
  'admin.supportReason': 'Warum Sie hinein müssen',
  'admin.supportReasonNote':
    'Dies wird dauerhaft aufgezeichnet, in ihrem Protokoll ebenso wie in unserem. Schreiben Sie, was Sie sie auch lesen lassen würden.',
  'admin.supportOpen': 'Zugriff öffnen',
  'admin.supportOpening': 'Wird geöffnet…',
  'admin.supportOpened':
    'Sie sind bis {time} in {tenant}. Öffnen Sie die Bildschirme des Autohauses in diesem Browser, um nachzusehen; schließen Sie den Besuch unten, wenn Sie fertig sind.',
  'admin.supportRecord': 'Das Protokoll',
  'admin.supportLoading': 'Protokoll wird geladen…',
  'admin.supportEmpty': 'Noch niemand war in einem Autohaus.',
  'admin.supportCaption': {
    one: '{count} Support-Besuch',
    other: '{count} Support-Besuche, neueste zuerst',
  },
  'admin.supportColWho': 'Wer',
  'admin.supportColWhy': 'Grund',
  'admin.supportColOpened': 'Geöffnet',
  'admin.supportColState': 'Zustand',
  'admin.supportCloseNow': 'Jetzt schließen',
  'admin.supportExpired': 'Abgelaufen',
  'admin.supportClosed': 'Geschlossen {date}',

  'nav.ageing': 'Wer uns schuldet',
  'nav.statements': 'Kontoauszüge',

  'ageing.title': 'Wer uns schuldet',
  'ageing.loading': 'Berechne, wer was schuldet…',
  'ageing.denied': 'Sie haben keinen Zugriff darauf.',
  'ageing.failed': 'Der Fälligkeitsbericht konnte nicht geladen werden.',
  'ageing.empty': 'Derzeit schuldet uns niemand etwas.',
  'ageing.colCustomer': 'Kunde',
  'ageing.colCurrent': 'Aktuell',
  'ageing.col31to60': '31–60 Tage',
  'ageing.col61to90': '61–90 Tage',
  'ageing.colOver90': 'Über 90 Tage',
  'ageing.colTotal': 'Summe',
  'ageing.totals': 'Summe',

  'statement.title': 'Kundenkontoauszug',
  'statement.customer': 'Kunde',
  'statement.from': 'Von',
  'statement.to': 'Bis',
  'statement.pickCustomer': 'Wählen Sie einen Kunden, um dessen Kontoauszug zu sehen.',
  'statement.loading': 'Berechne den Kontoauszug…',
  'statement.denied': 'Sie haben keinen Zugriff darauf.',
  'statement.failed': 'Der Kontoauszug konnte nicht geladen werden.',
  'statement.opening': 'Saldovortrag',
  'statement.closing': 'Offener Saldo',
  'statement.colDate': 'Datum',
  'statement.colReference': 'Referenz',
  'statement.colAmount': 'Betrag',
  'statement.colBalance': 'Saldo',
  'statement.kindInvoice': 'Berechnet',
  'statement.kindPayment': 'Bezahlt',
  'statement.empty': 'In diesem Zeitraum ist nichts passiert.',

  'customers.creditLimit': 'Kreditlimit',
  'customers.creditLimitNone': 'Kein Limit festgelegt',
  'customers.creditLimitEdit': 'Ändern',
  'customers.creditLimitPlaceholder': 'Kein Limit',
  'customers.creditLimitSave': 'Speichern',
  'customers.creditLimitSaving': 'Wird gespeichert…',
};
