// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   es — the Spanish catalogue.
//
// Usage:
//   Selected by `LANGUAGE es`. Typed as `Catalogue`, so a key added to en.ts
//   fails the build here until it has one too.
//
// Coding Instructions:
//   Neutral Spanish, not regional. This has to read naturally to a
//   dealership in Madrid, Mexico City and Buenos Aires, so where a word
//   splits — recambios / refacciones — the widely understood one wins:
//   "repuestos". Same reasoning behind "vehículo" over "coche" or "carro".
//
//   Spanish takes TWO plural forms, one and other, like English. It does
//   not need the four Russian has or the six Arabic does, but the entries
//   are still objects rather than a ternary at the call site — the shape is
//   the contract, and a catalogue that varies its shape by language cannot
//   be checked by the compiler.
//
//   Formal or informal address: this uses **usted** throughout, because the
//   reader is at work and the application is talking to them about their
//   employer's records. "Inicie sesión", never "inicia sesión".

import type { Catalogue } from '../index';

export const es: Catalogue = {
  // --- Words the whole application uses -------------------------------------
  'app.name': 'DealerFOSS',
  'common.loading': 'Cargando…',
  'common.save': 'Guardar',
  'common.saving': 'Guardando…',
  'common.cancel': 'Cancelar',
  'common.close': 'Cerrar',
  'confirm.typeToConfirm': 'Escriba {text} para confirmar.',
  'common.retry': 'Inténtelo de nuevo',
  'common.search': 'Buscar',
  'common.searching': 'Buscando…',
  'common.none': 'Ninguno',
  'common.all': 'Todos',
  'common.yes': 'Sí',
  'common.no': 'No',
  'common.back': 'Atrás',
  'common.continue': 'Continuar',
  'common.unexpected': 'Algo ha fallado. Inténtelo de nuevo.',
  'common.notPermitted': 'No tiene permiso para ver esto.',
  'common.unreachable': 'No se ha podido contactar con el servidor. ¿Está funcionando?',

  'record.opening': 'Abriendo el registro…',
  'record.unreachable':
    'No se puede abrir ese registro. Puede que se haya eliminado o que pertenezca a una parte del negocio que usted no puede ver.',
  'record.backToList': 'Volver a la lista',

  // --- Refusals the reader sees on any screen -------------------------------
  'error.network': 'No se ha podido contactar con el servidor. ¿Está funcionando?',
  'error.invalidCredentials': 'Ese correo y esa contraseña no corresponden a ninguna cuenta.',
  'error.sessionRequired': 'Inicie sesión para usar esto.',
  'error.adminSessionRequired':
    'Inicie sesión como administrador para entrar en la consola de la instalación. El acceso de un concesionario no llega hasta aquí.',
  'error.sessionInvalid': 'Su sesión ha terminado. Vuelva a iniciar sesión.',
  'error.antiForgeryFailed':
    'Este navegador ya no tiene un testigo que corresponda a su sesión. Vuelva a iniciar sesión.',
  'error.secondFactorRejected': 'Ese código no se ha aceptado.',
  'error.secondFactorRequired':
    'Su puesto exige la verificación en dos pasos. Configúrela para acceder al resto de la aplicación.',
  'error.mfaNotEnrolled': 'Esta cuenta no tiene configurada la verificación en dos pasos.',
  'error.mfaAlreadyOn': 'Esta cuenta ya tiene activada la verificación en dos pasos.',
  'error.notATenantCaller': 'Un administrador no puede actuar como usuario de un concesionario.',
  'error.tenantRequired': 'Indique de qué grupo de concesionarios se trata.',
  'error.tenantNotFound': 'Ningún grupo activo corresponde a ese nombre.',

  // --- The shell ------------------------------------------------------------
  'shell.skipToContent': 'Ir al contenido',
  'shell.mainNavigation': 'Principal',
  'shell.signOut': 'Cerrar sesión',
  'shell.shortcuts': 'Atajos',
  'shell.shortcutsTitle': 'Atajos de teclado ( ? )',
  'shell.language': 'Idioma',
  'shell.appearance': 'Apariencia',

  'nav.dashboard': 'Este mes',
  'nav.groupSales': 'Ventas',
  'nav.groupService': 'Taller',
  'nav.groupAccounting': 'Contabilidad',
  'nav.groupPeople': 'Personal y seguridad',
  'nav.customers': 'Clientes',
  'nav.leads': 'Consultas',
  'nav.deals': 'Operaciones',
  'nav.stock': 'Stock',
  'nav.workshop': 'Taller',
  'nav.parts': 'Repuestos',
  'nav.trialBalance': 'Balance de comprobación',
  'nav.books': 'La contabilidad',
  'nav.records': 'Registros',
  'nav.staff': 'Personas',
  'nav.secondFactor': 'Verificación en dos pasos',
  'nav.passkeys': 'Claves de acceso',

  // --- Appearance -----------------------------------------------------------
  'appearance.auto': 'Automática',
  'appearance.autoHint': 'Seguir a este equipo',
  'appearance.light': 'Clara',
  'appearance.lightHint': 'Siempre clara',
  'appearance.dark': 'Oscura',
  'appearance.darkHint': 'Siempre oscura',

  // --- Signing in -----------------------------------------------------------
  'signIn.lede': 'Inicie sesión en su concesionario.',
  'signIn.dealerGroup': 'Grupo de concesionarios',
  'signIn.email': 'Correo electrónico',
  'signIn.password': 'Contraseña',
  'signIn.submit': 'Iniciar sesión',
  'signIn.submitting': 'Iniciando sesión…',
  'signIn.codeLede':
    'Introduzca el código de seis dígitos de su aplicación de autenticación, o uno de sus códigos de recuperación.',
  'signIn.code': 'Código',
  'signIn.checking': 'Comprobando…',
  'signIn.startAgain': 'Empezar de nuevo',

  // --- Setting a first password ---------------------------------------------
  'setPassword.title': 'Establezca su contraseña',
  'setPassword.lede':
    'Su responsable le ha dado un código. Úselo una vez aquí para elegir una contraseña que solo usted conoce: nadie en el concesionario puede ver lo que escriba.',
  'setPassword.dealership': 'Concesionario',
  'setPassword.email': 'Correo electrónico',
  'setPassword.code': 'Código',
  'setPassword.password': 'Nueva contraseña',
  'setPassword.passwordHint':
    'Al menos 12 caracteres. Lo que hace difícil de adivinar una contraseña es su longitud.',
  'setPassword.again': 'Repita la nueva contraseña',
  'setPassword.mismatch': 'Las dos no coinciden.',
  'setPassword.failed': 'No ha funcionado.',
  'setPassword.submit': 'Establecer mi contraseña',
  'setPassword.doneTitle': 'Ya está listo',
  'setPassword.doneLede':
    'Inicie sesión con su correo electrónico y la contraseña que acaba de elegir.',
  'setPassword.toSignIn': 'Ir a iniciar sesión',

  // --- Two-step sign-in -----------------------------------------------------
  'secondFactor.title': 'Verificación en dos pasos',
  'secondFactor.required':
    'Su concesionario exige la verificación en dos pasos para su puesto. Hasta que la configure, esta es la única pantalla que puede usar.',
  'secondFactor.intro':
    'A partir de ahora, al iniciar sesión se le pedirá un código de seis dígitos de una aplicación del móvil además de su contraseña. Google Authenticator, Authy y 1Password funcionan todas.',
  'secondFactor.start': 'Empezar',
  'secondFactor.starting': 'Empezando…',
  'secondFactor.pointApp': 'Apunte su aplicación de autenticación a este cuadro.',
  'secondFactor.qrTitle': 'Escanee esto con su aplicación de autenticación',
  'secondFactor.cannotScan': '¿No puede escanearlo?',
  'secondFactor.typeInstead': 'Escriba esto a mano en la aplicación:',
  'secondFactor.enterCode': 'Ahora introduzca el código que muestre',
  'secondFactor.turnOn': 'Activarla',
  'secondFactor.checking': 'Comprobando…',
  'secondFactor.notYet':
    'Todavía no ha cambiado nada sobre el inicio de sesión. Solo surte efecto cuando se acepte el código de arriba.',
  'secondFactor.onNow':
    'La verificación en dos pasos está activada. A partir de ahora se le pedirá un código después de la contraseña.',
  'secondFactor.saveTitle': 'Guarde esto en un sitio seguro',
  'secondFactor.saveLede':
    'Cada uno funciona una sola vez, y solo si pierde el móvil. Esta es la única vez que se mostrarán.',
  'secondFactor.recoveryCodes': 'Códigos de recuperación',

  // --- Keyboard shortcuts ---------------------------------------------------
  'shortcuts.title': 'Atajos de teclado',
  'shortcuts.space': 'Espacio',
  'shortcuts.note':
    'Los atajos se ignoran mientras escribe en un campo, así que nunca se comen un carácter que quería escribir.',
  'shortcuts.goDashboard': 'Ir al panel del mes',
  'shortcuts.goStock': 'Ir al stock',
  'shortcuts.goCustomers': 'Ir a clientes',
  'shortcuts.goLeads': 'Ir a consultas',
  'shortcuts.goDeals': 'Ir a operaciones',
  'shortcuts.goWorkshop': 'Ir al taller',
  'shortcuts.goParts': 'Ir a repuestos',
  'shortcuts.goBooks': 'Ir a la contabilidad',
  'shortcuts.monthBefore': 'En el panel: el mes anterior',
  'shortcuts.monthAfter': 'En el panel: el mes siguiente',
  'shortcuts.thisMonth': 'En el panel: volver a este mes',
  'shortcuts.showList': 'Mostrar esta lista',

  // --- Words the API sends back ---------------------------------------------
  'enum.inventoryStatus.Incoming': 'En camino',
  'enum.inventoryStatus.Reconditioning': 'En preparación',
  'enum.inventoryStatus.Available': 'Disponible',
  'enum.inventoryStatus.OnHold': 'Reservado',
  'enum.inventoryStatus.Sold': 'Vendido',
  'enum.inventoryStatus.Removed': 'Retirado',

  'enum.leadStatus.New': 'Nueva',
  'enum.leadStatus.Working': 'En curso',
  'enum.leadStatus.Appointment': 'Con cita',
  'enum.leadStatus.Won': 'Ganada',
  'enum.leadStatus.Lost': 'Perdida',

  'enum.leadSource.WalkIn': 'Visita directa',
  'enum.leadSource.Phone': 'Teléfono',
  'enum.leadSource.Website': 'Sitio web',
  'enum.leadSource.Referral': 'Recomendación',
  'enum.leadSource.Marketplace': 'Anuncio en portal',
  'enum.leadSource.Unknown': 'Sin registrar',

  'enum.dealStatus.Draft': 'Borrador',
  'enum.dealStatus.Submitted': 'Enviada',
  'enum.dealStatus.Approved': 'Aprobada',
  'enum.dealStatus.Delivered': 'Entregada',
  'enum.dealStatus.Lost': 'Perdida',

  'enum.chargeKind.VehiclePrice': 'Precio del vehículo',
  'enum.chargeKind.Fee': 'Gasto',
  'enum.chargeKind.Discount': 'Descuento',
  'enum.chargeKind.Accessory': 'Accesorio',

  'enum.repairOrderStatus.Booked': 'Con cita',
  'enum.repairOrderStatus.InProgress': 'En curso',
  'enum.repairOrderStatus.Completed': 'Terminada',
  'enum.repairOrderStatus.Invoiced': 'Facturada',
  'enum.repairOrderStatus.Cancelled': 'Anulada',

  'enum.serviceLineKind.Labour': 'Mano de obra',
  'enum.serviceLineKind.Part': 'Repuesto',
  'enum.serviceLineKind.Sublet': 'Trabajo externo',

  'enum.servicePayType.CustomerPay': 'Paga el cliente',
  'enum.servicePayType.Warranty': 'Garantía',
  'enum.servicePayType.Internal': 'Interno',

  'enum.financeProductKind.Warranty': 'Garantía',
  'enum.financeProductKind.Gap': 'GAP',
  'enum.financeProductKind.ServicePlan': 'Plan de mantenimiento',
  'enum.financeProductKind.Protection': 'Protección',
  'enum.financeProductKind.Other': 'Otro',

  'enum.periodState.Open': 'Abierto',
  'enum.periodState.Closed': 'Cerrado',

  'enum.booksState.NotOpened': 'Sin abrir',
  'enum.booksState.Open': 'Abierta',
  'enum.booksState.Closed': 'Cerrada',
  'enum.booksState.Unknown': 'Desconocida',

  'enum.importKind.Customers': 'Clientes',
  'enum.importKind.Vehicles': 'Vehículos',

  'enum.tenantStatus.Active': 'Activo',
  'enum.tenantStatus.Suspended': 'Suspendido',
  'enum.tenantStatus.Provisioning': 'En creación',
  'enum.tenantStatus.Archived': 'Archivado',

  // --- Stock ----------------------------------------------------------------
  'stock.title': 'Stock',
  'stock.status': 'Estado',
  'stock.loading': 'Cargando el stock…',
  'stock.denied':
    'No tiene acceso al stock de esta sede. Pregunte a un responsable si cree que es un error.',
  'stock.failed': 'No se ha podido cargar el stock.',
  'stock.empty': 'Todavía no hay nada. Los vehículos aparecen al darlos de alta en stock.',
  'stock.onlyStockNumber': 'Mostrando solo el n.º de stock {stock}.',
  'stock.showEverything': 'Mostrar todo',
  'stock.colStock': 'N.º stock',
  'stock.colVehicle': 'Vehículo',
  'stock.colVin': 'Bastidor',
  'stock.colStatus': 'Estado',
  'stock.count': { one: '{count} vehículo en stock', other: '{count} vehículos en stock' },
  'stock.detailFor': 'N.º de stock {stock}',
  'stock.cost': 'Coste',
  'stock.costUnknown': 'sin registrar',
  'stock.acquired': 'Dado de alta',
  'stock.historyTitle': 'Qué le ha pasado',
  'stock.historyEmpty': 'Todavía no se ha registrado nada sobre este vehículo.',
  'stock.takenIn': 'Dado de alta en stock como {to}',
  'stock.moved': '{from} → {to}',

  'nav.reports': 'Informes',

  'reports.title': 'Lo que ha dado el mes',
  'reports.month': 'Mes',
  'reports.loading': 'Calculando las cifras…',
  'reports.denied': 'No tienes acceso a las cifras. Habla con quien lleva la contabilidad.',
  'reports.profitTitle': 'Pérdidas y ganancias',
  'reports.department': 'Departamento',
  'reports.revenue': 'Ingresos',
  'reports.cost': 'Coste',
  'reports.gross': 'Bruto',
  'reports.grossProfit': 'Beneficio bruto',
  'reports.overheads': 'Lo que cuesta mantener el negocio',
  'reports.totalOverheads': 'Total de gastos',
  'reports.netProfit': 'Beneficio neto',
  'reports.sheetTitle': 'Lo que vale el negocio',
  'reports.sheetBalances': 'Cuadra: lo que el negocio tiene es igual a lo que debe más lo que vale.',
  'reports.sheetDoesNotBalance':
    'Esto no cuadra. Se ha registrado algo que este informe no sabe clasificar, así que trata todas las cifras de abajo como dudosas hasta que alguien averigüe qué es.',
  'reports.assets': 'Lo que tiene',
  'reports.liabilities': 'Lo que debe',
  'reports.equity': 'Lo que aportaron los propietarios',
  'reports.total': 'Total',
  'reports.nothingHere': 'Aquí no hay nada registrado todavía.',
  'reports.earningsToDate': 'Ganado desde el principio',
  'reports.earningsNote':
    'Se mantiene como línea aparte y no se suma a lo aportado, porque aún no se ha cerrado ningún ejercicio.',

  'entry.title': 'Registrar un asiento',
  'entry.note':
    'Para lo que no registra nada más: un gasto, el capital aportado, una corrección. Los dos lados tienen que sumar lo mismo.',
  'entry.whichLocation': 'Qué ubicación',
  'entry.chooseLocation': 'Elige una ubicación…',
  'entry.when': 'Cuándo ocurrió',
  'entry.what': 'Para qué es',
  'entry.account': 'Cuenta',
  'entry.chooseAccount': 'Elige una cuenta…',
  'entry.debit': 'Debe',
  'entry.credit': 'Haber',
  'entry.lineNote': 'Nota',
  'entry.accountOnLine': 'Cuenta en la línea {line}',
  'entry.debitOnLine': 'Debe en la línea {line}',
  'entry.creditOnLine': 'Haber en la línea {line}',
  'entry.noteOnLine': 'Nota en la línea {line}',
  'entry.totals': 'Totales',
  'entry.addLine': 'Añadir una línea',
  'entry.outBy': 'Los dos lados difieren en {amount}.',
  'entry.record': 'Registrarlo',

  'enum.paymentMethod.Cash': 'Efectivo',
  'enum.paymentMethod.Card': 'Tarjeta',
  'enum.paymentMethod.BankTransfer': 'Transferencia',
  'enum.paymentMethod.Cheque': 'Cheque',
  'enum.paymentMethod.Finance': 'Financiera',
  'enum.paymentMethod.CustomerCredit': 'Saldo a favor',

  'money.title': 'Lo que se debe',
  'money.billed': 'Facturado',
  'money.paid': 'Pagado hasta ahora',
  'money.outstanding': 'Pendiente',
  'money.settled': 'Pagado por completo. No queda nada pendiente.',
  'money.owedFor': {
    one: 'Pendiente desde hace {count} día.',
    other: 'Pendiente desde hace {count} días.',
  },
  'money.paymentsTitle': 'Lo que se ha pagado',
  'money.howMuch': 'Cuánto',
  'money.howPaid': 'Cómo ha pagado',
  'money.reference': 'Referencia (queda en el registro)',
  'money.takeIt': 'Registrar el pago',
  'picker.change': 'Cambiar',
  'picker.typeToSearch': 'Escriba para buscar',
  'picker.searching': 'Buscando…',
  'picker.searchFailed': 'No se pudo realizar la búsqueda. Inténtelo de nuevo.',
  'picker.noMatches': 'No hay coincidencias.',
  'picker.startTyping': 'Escriba unas letras para encontrarlo.',
  'money.willOverpay':
    'Eso es {extra} más de lo que se debe. La diferencia queda como saldo a favor del cliente.',
  'money.creditUsable': 'Este cliente tiene saldo a su favor',
  'money.creditUsableNote':
    'Pagó de más en algo anterior. Puede descontarse de esta factura en lugar de devolverse.',
  'money.useItHere': 'Aplicar {amount} a esta factura',
  'money.creditTitle': 'Se le debe al cliente',
  'money.creditNote':
    'Este dinero es suyo y se guarda aquí hasta usarlo o devolverlo. No es del concesionario.',
  'money.creditFrom': 'pagado de más el {date}',
  'money.refundHow': 'Cómo se devuelve',
  'money.giveItBack': 'Devolverlo',
  'stock.takeItIn': 'Dar entrada a un coche',
  'stock.confirmTakeIn': 'Dar entrada',
  'stock.takeInTitle': 'Dar entrada a un coche',
  'stock.takeInNote':
    'El coche y su ficha se crean juntos, porque un coche que llega casi siempre es uno que no habías visto nunca.',
  'stock.whichLocation': 'Qué ubicación',
  'stock.chooseLocation': 'Elige una ubicación…',
  'stock.vinOptional': 'VIN (opcional)',
  'stock.modelYear': 'Año',
  'stock.make': 'Marca',
  'stock.model': 'Modelo',
  'stock.trimOptional': 'Acabado (opcional)',
  'stock.costOptional': 'Lo que costó (opcional)',
  'stock.costNote':
    'Un coste pone el coche en el balance. Déjalo vacío si aún no lo sabes: se registra como desconocido, no como cero.',
  'stock.floorplanned': 'Una financiera paga este coche (floorplan)',
  'stock.moveTitle': 'Adónde va ahora',
  'stock.moveNote': 'Nota (queda en el registro)',
  'stock.moveTo': 'Pasar a {to}',
  'stock.soldNote': 'Este coche se ha vendido. Revierte la operación para deshacerlo.',
  'stock.noMovesNote': 'Este coche no puede moverse a ningún sitio desde aquí.',

  'enum.accountKind.Asset': 'Activo',
  'enum.accountKind.Liability': 'Pasivo',
  'enum.accountKind.Equity': 'Patrimonio neto',
  'enum.accountKind.Revenue': 'Ingreso',
  'enum.accountKind.Expense': 'Gasto',

  // --- Trial balance --------------------------------------------------------
  'trialBalance.title': 'Balance de comprobación',
  'trialBalance.loading': 'Sumando…',
  'trialBalance.denied': 'No tiene acceso a estas cifras.',
  'trialBalance.failed': 'No se han podido cargar los saldos.',
  'trialBalance.empty':
    'Todavía no hay nada contabilizado. Los asientos aparecen aquí en cuanto se entrega un vehículo.',
  'trialBalance.inBalance': 'Cuadrado: el debe y el haber suman ambos {total}.',
  'trialBalance.outOfBalance': 'Descuadrado en {difference}. Algo se ha perdido por el camino.',
  'trialBalance.colCode': 'Código',
  'trialBalance.colAccount': 'Cuenta',
  'trialBalance.colKind': 'Tipo',
  'trialBalance.colDebits': 'Debe',
  'trialBalance.colCredits': 'Haber',
  'trialBalance.colBalance': 'Saldo',
  'trialBalance.total': 'Total',

  // --- Customers ------------------------------------------------------------
  'enum.customerKind.Person': 'Particular',
  'enum.customerKind.Business': 'Empresa',

  'enum.contactKind.Email': 'Correo electrónico',
  'enum.contactKind.Phone': 'Teléfono',
  'enum.contactKind.Mobile': 'Móvil',

  'customers.title': 'Clientes',
  'customers.find': 'Buscar a alguien',
  'customers.findPlaceholder': 'Nombre, teléfono o correo',
  'customers.add': 'Añadir un cliente',
  'customers.looking': 'Buscando…',
  'customers.denied':
    'No tiene acceso a las fichas de clientes. Pregunte a un responsable si cree que es un error.',
  'customers.failed': 'No se han podido cargar los clientes.',
  'customers.noMatches': 'No hay nadie que coincida.',
  'customers.colName': 'Nombre',
  'customers.colKind': 'Tipo',
  'customers.colEmail': 'Correo',
  'customers.colPhone': 'Teléfono',
  'customers.count': { one: '{count} cliente', other: '{count} clientes' },

  'customers.kindLabel': 'Particular o empresa',
  'customers.firstName': 'Nombre',
  'customers.lastName': 'Apellidos',
  'customers.businessName': 'Razón social',
  'customers.email': 'Correo electrónico',
  'customers.phone': 'Teléfono',
  'customers.submit': 'Añadir',
  'customers.checking': 'Comprobando duplicados…',
  'customers.adding': 'Añadiendo…',

  'customers.duplicateTitle': 'Aquí ya hay alguien parecido',
  'customers.duplicateLede':
    'Crear una segunda ficha de la misma persona parte su historial: su taller, sus operaciones y sus datos de contacto dejan de coincidir. Compruebe si alguno de estos es la misma persona.',
  'customers.noContactDetails': 'sin datos de contacto',
  'customers.cameFrom': 'Procede de',
  'customers.waysToReach': 'Cómo localizarle',
  'customers.primary': 'principal',
  'customers.address': 'Dirección',
  'customers.noAddress': 'No hay ninguna dirección registrada.',
  'customers.oneOfTheseIsThem': 'Es uno de estos',
  'customers.addAnyway': 'Ninguno: añadir de todas formas',

  // --- Bringing records in and taking them out ------------------------------
  'enum.importOutcome.Pending': 'Pendiente',
  'enum.importOutcome.Created': 'Añadido',
  'enum.importOutcome.Updated': 'Ya estaba',
  'enum.importOutcome.Skipped': 'Omitido',
  'enum.importOutcome.Failed': 'Rechazado',

  'records.title': 'Registros',
  'records.bringIn': 'Traer registros',
  'records.bringInLede':
    'Una hoja de cálculo exportada de su sistema anterior, guardada como CSV. No se escribe nada hasta que lo haya ejecutado antes como prueba.',
  'records.whatIsInIt': 'Qué contiene el archivo',
  'records.file': 'El archivo',
  'records.unreadableFile': 'No se ha podido leer ese archivo. ¿Es un CSV de texto?',
  'records.practice': 'Prueba',
  'records.practising': 'Probando…',
  'records.importForReal': 'Importar de verdad',
  'records.importing': 'Importando…',
  'records.practiseFirst':
    'Haga primero la prueba. No cambia nada y le dice exactamente qué hará la importación real.',

  'records.takeOut': 'Llevarse los registros',
  'records.takeOutLede':
    'Descarga todo lo de ese tipo en CSV. Tiene la misma forma que esta pantalla acepta de vuelta, así que puede llevárselo a donde quiera, incluso a otro sistema distinto.',
  'records.downloadCustomers': 'Descargar clientes',
  'records.downloadVehicles': 'Descargar vehículos',

  'records.couldNotRun': 'No se ha podido ejecutar esa importación.',
  'records.whatWouldHappen': 'Qué pasaría',
  'records.whatHappened': 'Qué ha pasado',
  'records.summaryPractice': {
    one: 'De {count} fila: se añadirían {created}, {updated} ya están, {skipped} omitidas, {failed} ilegibles.',
    other:
      'De {count} filas: se añadirían {created}, {updated} ya están, {skipped} omitidas, {failed} ilegibles.',
  },
  'records.summaryReal': {
    one: 'De {count} fila: {created} añadidas, {updated} ya estaban, {skipped} omitidas, {failed} rechazadas.',
    other:
      'De {count} filas: {created} añadidas, {updated} ya estaban, {skipped} omitidas, {failed} rechazadas.',
  },
  'records.nothingWritten': 'No se ha escrito nada. Esto era una prueba.',
  'records.rowsToLookAt': 'Filas que hay que mirar',
  'records.rowsToLookAtLede':
    'El número de línea es el que ve en su hoja de cálculo, y la fila se cita exactamente como llegó. Corrija el archivo y vuelva a ejecutarlo: nada de esto modifica lo que usted envió.',
  'records.problemCount': {
    one: '{count} fila que necesita atención',
    other: '{count} filas que necesitan atención',
  },
  'records.colLine': 'Línea',
  'records.colWhatHappened': 'Qué ha pasado',
  'records.colTheRow': 'La fila',
  'records.timeout':
    'Esa importación está tardando más de lo esperado. Sigue en marcha: esta pantalla solo ha dejado de esperar.',

  // --- The books ------------------------------------------------------------
  'periods.title': 'La contabilidad',
  'periods.loading': 'Cargando la contabilidad…',
  'periods.denied': 'No tiene acceso a las cuentas.',
  'periods.failed': 'No se ha podido leer la contabilidad.',
  'periods.actionFailed': 'No ha funcionado.',
  'periods.lede':
    'No se puede contabilizar nada en un mes hasta que su contabilidad esté abierta, ni en uno que se haya cerrado. Cerrar es algo que se hace cuando termina el trabajo de cierre: no hay ninguna fecha que lo haga por usted.',
  'periods.none': 'Todavía no hay ningún mes abierto. No se puede contabilizar nada hasta abrir uno.',

  'periods.openAMonth': 'Abrir un mes',
  'periods.openLede':
    'Hasta que un mes esté abierto, no se puede contabilizar nada con esa fecha: una venta o una factura de taller se rechazarán. Abrirlo es deliberado, para que la contabilidad tenga el inicio que usted elija y no el que se deduzca de lo primero que alguien escribió.',
  'periods.year': 'Año',
  'periods.month': 'Mes',
  'periods.openIt': 'Abrirlo',

  'periods.reopenTitle': '¿Reabrir {month}?',
  'periods.reopenLede':
    'Este mes está cerrado y puede que sus cifras ya se hayan comunicado. La reapertura queda registrada en el mes junto con su motivo, para que quien lo mire más adelante vea qué pasó y por qué.',
  'periods.reopenWhy': '¿Por qué se reabre?',
  'periods.reopenPlaceholder': 'Llegó una factura de proveedor el día 4',
  'periods.reopenIt': 'Reabrirlo',
  'periods.leaveClosed': 'Dejarlo cerrado',
  'periods.reopen': 'Reabrir',

  'periods.closeIt': 'Cerrarlo',
  'periods.closeTitle': '¿Cerrar {month}?',
  'periods.confirmClose': 'No se podrá contabilizar nada más hasta que se reabra.',

  'periods.caption': 'Todos los meses de la contabilidad, del más reciente al más antiguo.',
  'periods.colMonth': 'Mes',
  'periods.colCutoff': 'Corte',
  'periods.colEntries': 'Asientos',
  'periods.colState': 'Estado',

  'periods.historyTitle': 'Qué le ha pasado a la contabilidad',
  'periods.wasOpened': '{month} abierto',
  'periods.wasClosed': '{month} cerrado',
  'periods.wasReopened': '{month} reabierto',

  // --- Enquiries ------------------------------------------------------------
  'leads.title': 'Consultas',
  'leads.show': 'Mostrar',
  'leads.stillChasing': 'Todavía en seguimiento',
  'leads.everything': 'Todo',
  'leads.onlyMine': 'Solo las mías',
  'leads.take': 'Registrar una consulta',
  'leads.untouchedTitle': 'Nadie está siguiendo estas',
  'leads.untouchedNote': {
    one: '{count} consulta no tiene a nadie asignado.',
    other: '{count} consultas no tienen a nadie asignado.',
  },
  'leads.noParticularCar': 'ningún vehículo concreto',
  'leads.waitingDays': {
    one: 'esperando {count} día',
    other: 'esperando {count} días',
  },
  'leads.loading': 'Cargando las consultas…',
  'leads.denied':
    'No tiene acceso a las consultas de esta sede. Pregunte a un responsable si cree que es un error.',
  'leads.failed': 'No se han podido cargar las consultas.',
  'leads.openFailed': 'No se ha podido abrir esa consulta.',
  'leads.empty':
    'Aquí no hay consultas. Una empieza en cuanto alguien llama o entra en la exposición.',

  'leads.colCustomer': 'Cliente',
  'leads.colAskedAbout': 'Preguntó por',
  'leads.colCameFrom': 'Procede de',
  'leads.colDays': 'Días',
  'leads.colChasedBy': 'La sigue',
  'leads.colStage': 'Fase',
  'leads.nothingSpecific': 'Nada concreto',
  'leads.nobodyYet': 'Todavía nadie',
  'leads.you': 'Usted',
  'leads.somebodyElse': 'Otra persona',
  'leads.count': { one: '{count} consulta', other: '{count} consultas' },
  'paging.showingRange': 'Mostrando {first}–{last} de {total}.',
  'paging.previous': 'Anteriores',
  'paging.next': 'Siguientes',

  'leads.cameIn': 'entró el {date}',
  'leads.unclaimed': 'Todavía nadie se ha hecho cargo de esta.',
  'leads.yoursToChase': 'Usted está siguiendo esta.',
  'leads.theirsToChase': '{name} está siguiendo esta.',
  'leads.putBack': 'Devolverla al grupo',
  'leads.iWillChase': 'La sigo yo',
  'leads.takeItOver': 'Hacerme cargo',
  'leads.handTo': 'Pasar a',
  'leads.chooseColleague': 'Elija un compañero',
  'leads.buildTheDeal': 'Preparar la operación',
  'leads.whatHappened': 'Qué ha pasado',
  'leads.finished':
    'Esta consulta está terminada. Un cliente que vuelva más adelante empieza una nueva.',
  'leads.note': 'Nota (queda en el registro)',
  'leads.notePlaceholder': 'Le dejé un mensaje · viene el sábado · compró en otro sitio',
  'leads.reopenedHere':
    'Una consulta perdida que vuelve se reabre aquí en lugar de volver a escribirse, para que el primer intento siga formando parte de la historia.',

  'leads.moveReopen': 'Reabrirla',
  'leads.moveStartChasing': 'Empezar el seguimiento',
  'leads.moveAppointment': 'Va a venir',
  'leads.moveWon': 'Va a comprar',
  'leads.moveLost': 'Marcar como perdida',

  'leads.captureTitle': 'Registrar una consulta',
  'leads.findCustomer': 'Buscar al cliente',
  'leads.whoIsAsking': 'Quién pregunta',
  'leads.chooseSomebody': 'Elija a alguien…',
  'leads.searchAboveNote':
    'Búsquelo arriba. Una consulta tiene que pertenecer a alguien, así que si es nuevo créelo antes en la pantalla de clientes.',
  'leads.notOnFile': '¿No está en la base? Añádelo aquí.',
  'leads.newCustomerTitle': 'Alguien nuevo',
  'leads.addAndUse': 'Añadir y usarlo',
  'leads.whichLocation': 'Qué sede',
  'leads.chooseLocation': 'Elija una sede…',
  'leads.onlyLocation': 'Esta consulta pertenece a {name} ({code}), la única sede en la que trabaja.',
  'leads.howTheyReachedUs': 'Cómo nos ha contactado',
  'leads.carAskedAbout': 'Vehículo por el que preguntó (opcional)',
  'leads.whatTheySaid': 'Qué ha dicho',
  'leads.whatTheySaidPlaceholder': 'Presupuesto, vehículo a cambio, para cuándo lo necesita…',
  'leads.save': 'Guardar la consulta',
  'leads.locationsFailed': 'No se han podido cargar sus sedes.',
  'leads.lookupFailed': 'No se ha podido consultar eso.',
  'leads.saveFailed': 'No se ha podido guardar esa consulta.',

  // --- The deal desk --------------------------------------------------------
  'deals.title': 'Operaciones',
  'deals.show': 'Mostrar',
  'deals.stillWorked': 'Todavía en curso',
  'deals.everything': 'Todo',
  'deals.start': 'Empezar una operación',
  'deals.loading': 'Cargando las operaciones…',
  'deals.denied':
    'No tiene acceso a las operaciones de esta sede. Pregunte a un responsable si cree que es un error.',
  'deals.failed': 'No se han podido cargar las operaciones.',
  'deals.openFailed': 'No se ha podido abrir esa operación.',
  'deals.empty': 'Aquí no hay operaciones. Una empieza cuando se pone precio a un vehículo para alguien.',
  'deals.documentFailed': 'No se ha podido abrir el documento.',

  'deals.colCustomer': 'Cliente',
  'deals.colVehicle': 'Vehículo',
  'deals.colStock': 'N.º stock',
  'deals.colDue': 'A pagar',
  'deals.colStage': 'Fase',
  'deals.count': { one: '{count} operación', other: '{count} operaciones' },

  'deals.printOrder': 'Imprimir el pedido',
  'deals.stockLine': 'N.º stock {stock}',
  'deals.numbersCaption': 'Las cifras de esta operación',
  'deals.colLine': 'Línea',
  'deals.colDescription': 'Descripción',
  'deals.colAmount': 'Importe',
  'deals.lineProduct': 'Producto',
  'deals.lineTradeIn': 'Vehículo a cambio',
  'deals.owesMore': 'debe más de lo que vale',
  'deals.dueFromCustomer': 'A pagar por el cliente',
  'deals.frozen':
    'Las cifras están congeladas. Dejaron de poder editarse al enviar esta operación, para que lo que aprueba un responsable sea lo que se le puso delante.',
  'deals.whatHappened': 'Qué ha pasado',

  'deals.finished': 'Esta operación está terminada. No puede pasarle nada más.',
  'deals.sendToManager': 'Enviar a un responsable',
  'deals.approve': 'Aprobar',
  'deals.handOver': 'Entregar el vehículo',
  'deals.markLost': 'Marcar como perdida',
  'deals.markedLostNote': 'Marcada como perdida desde la mesa de operaciones.',
  'deals.cannotApproveOwn':
    'Quien prepara una operación no puede ser quien la aprueba. Si es usted, tiene que hacerlo un responsable.',

  'deals.soldWithTheCar': 'Vendido con el vehículo',
  'deals.colProduct': 'Producto',
  'deals.colPrice': 'Precio',
  'deals.colGross': 'Margen',
  'deals.productGross': '{amount} de margen en lo vendido con el vehículo.',
  'deals.cancelProduct': 'Cancelar',
  'deals.productCancelled': 'Cancelado el {date}, {refund} devuelto como crédito',
  'deals.cancelProductTitle': 'Cancelar {product}',
  'deals.cancelProductNote': 'Se puede acreditar hasta {max} al cliente.',
  'deals.refundAmount': 'Importe a devolver',
  'deals.cancelReason': 'Motivo (opcional)',
  'deals.confirmCancelProduct': 'Cancelar el producto',

  // --- Deal terms (the charges editor) --------------------------------------
  'terms.title': 'Las cifras',
  'terms.caption': 'Los conceptos de esta operación',
  'terms.colLine': 'Línea',
  'terms.colDescription': 'Descripción',
  'terms.colAmount': 'Importe',
  'terms.remove': 'Quitar',
  'terms.lineKind': 'Tipo de la línea {n}',
  'terms.lineDescription': 'Descripción de la línea {n}',
  'terms.lineAmount': 'Importe de la línea {n}',
  'terms.addLine': 'Añadir una línea',
  'terms.addTradeIn': 'Añadir un vehículo a cambio',
  'terms.dropTradeIn': 'Al final no hay vehículo a cambio',
  'terms.tradeInTitle': 'El vehículo a cambio',
  'terms.whatTheyTrade': 'Qué entrega',
  'terms.whatWeAllow': 'Cuánto le damos por él',
  'terms.whatIsOwed': 'Cuánto debe todavía por él',
  'terms.negativeEquity':
    'Debe por él más de lo que le damos, así que la diferencia se añade a esta operación.',
  'terms.save': 'Guardar las cifras',
  'terms.rejected': 'Esas cifras no se han aceptado.',
  'terms.needsPrice':
    'Toda operación necesita un precio para el propio vehículo antes de poder guardarse.',

  // --- F&I products ---------------------------------------------------------
  'products.title': 'Vendido con el vehículo',
  'products.loading': 'Cargando lo que puede venderse…',
  'products.none':
    'No hay productos configurados para vender. Un responsable los añade en el catálogo financiero.',
  'products.lede':
    'Los precios parten del catálogo y usted puede cambiarlos: lo que escriba aquí es lo que queda registrado en esta operación, y los cambios posteriores de tarifa no lo tocarán.',
  'products.colSell': 'Vender',
  'products.colProduct': 'Producto',
  'products.colPrice': 'Precio',
  'products.colCost': 'Coste',
  'products.colGross': 'Margen',
  'products.sellThis': 'Vender {product}',
  'products.priceFor': 'Precio de {product}',
  'products.costOf': 'Coste de {product}',
  'products.termMonths': { one: '{count} mes', other: '{count} meses' },
  'products.withdrawn': 'ya no se ofrece',
  'products.nothingSelected': 'No hay nada seleccionado.',
  'products.addedToDeal': '{added} añadidos a la operación, con {gross} de margen.',
  'products.saveFailed': 'No se ha guardado.',
  'products.saveWhatIsSold': 'Guardar lo que se vende',

  // --- Starting a deal ------------------------------------------------------
  'tax.title': 'Impuestos',
  'tax.lede':
    'Todavía nada calcula esto por usted, así que escriba lo que corresponda. Cada línea deja constancia de que la introdujo una persona, que es lo que un gerente y un auditor necesitan ver después.',
  'tax.workedOutFrom': 'Dirección con la que se calculan los impuestos',
  'tax.state': 'Estado o región',
  'tax.county': 'Condado',
  'tax.postalCode': 'Código postal',
  'tax.country': 'País',
  'tax.colDescription': 'Impuesto',
  'tax.colJurisdiction': 'Jurisdicción',
  'tax.colBasis': 'Base imponible',
  'tax.colRate': 'Tipo %',
  'tax.colAmount': 'Importe',
  'tax.colSource': 'De dónde viene',
  'tax.descriptionOfLine': 'Impuesto en la línea {line}',
  'tax.jurisdictionOfLine': 'Jurisdicción en la línea {line}',
  'tax.basisOfLine': 'Base imponible en la línea {line}',
  'tax.rateOfLine': 'Tipo en la línea {line}, en porcentaje',
  'tax.amountOfLine': 'Impuesto cobrado en la línea {line}',
  'tax.none': 'Todavía no hay impuestos en esta operación.',
  'tax.totalIs': 'Impuestos de esta operación: {total}.',
  'tax.totalLabel': 'Impuestos',
  'tax.addLine': 'Añadir una línea de impuesto',
  'tax.save': 'Guardar los impuestos',
  'tax.from.EnteredByPerson': 'la introdujo una persona',
  'tax.from.Pack': 'una tabla de tipos',
  'tax.from.Vendor': 'un proveedor fiscal',
  'tax.fromPack': '{pack} v{version}',

  'startDeal.title': 'Empezar una operación',
  'startDeal.findBuyer': 'Buscar al comprador',
  'startDeal.buyer': 'Quién compra',
  'startDeal.chooseBuyer': 'Elija a alguien…',
  'startDeal.searchAbove': 'Búsquelo arriba. Si es nuevo, créelo en la pantalla de clientes.',
  'startDeal.fromEnquiry': 'De la consulta de {name}. La operación quedará enlazada con ella.',
  'startDeal.thatCustomer': 'ese cliente',
  'startDeal.whichCar': 'Qué vehículo',
  'startDeal.chooseCar': 'Elija un vehículo…',
  'startDeal.nothingAvailable':
    'Ahora mismo no hay nada disponible en la exposición. Un vehículo que ya está en otra operación queda reservado hasta que esa operación termine.',
  'startDeal.chooseCarFirst': 'Elija primero un vehículo.',
  'startDeal.submit': 'Empezar la operación',
  'startDeal.starting': 'Empezando…',
  'startDeal.failed': 'No se ha podido empezar esa operación.',
  'startDeal.stockFailed': 'No se ha podido cargar el stock.',
  'startDeal.customerFailed': 'No se ha podido leer ese cliente.',

  // --- People ---------------------------------------------------------------
  'staff.title': 'Personas',
  'staff.loading': 'Cargando quién trabaja aquí…',
  'staff.denied': 'No tiene acceso a la lista de personal. Pregunte a un responsable si la necesita.',
  'staff.failed': 'No se ha podido leer la lista de personal.',
  'staff.actionFailed': 'No ha funcionado.',
  'staff.empty': 'Todavía no hay nadie.',
  'staff.add': 'Añadir a alguien',
  'staff.caption': 'Todo el mundo cuyo acceso alcanza una sede en la que usted trabaja.',
  'staff.colName': 'Nombre',
  'staff.colEmail': 'Correo',
  'staff.colHolds': 'Tiene',
  'staff.colSecondFactor': 'Segundo factor',
  'staff.colState': 'Estado',
  'staff.holdsNothing': 'Todavía nada',

  'staff.stateStopped': 'Bloqueada',
  'staff.stateAwaiting': 'Pendiente de primera contraseña',
  'staff.stateWorking': 'Activa',

  'staff.codeFor': 'Código para {name}',
  'staff.readItOut':
    'Léaselo en voz alta. Con él establece su propia contraseña en la pantalla de inicio de sesión: nadie más la escribe nunca, usted incluido.',
  'staff.onlyTimeShown': 'Esta es la única vez que puede mostrarse.',
  'staff.onlyTimeShownRest':
    'Solo se guarda un resumen cifrado, así que no puede volver a consultarse. Si se pierde, emita uno nuevo, lo que anula este. Caduca {expires}.',
  'staff.passedItOn': 'Ya se lo he dado',

  'staff.addTitle': 'Añadir a alguien',
  'staff.addLede':
    'No podrá iniciar sesión hasta que establezca una contraseña con el código que esto genera. Usted nunca ve ni elige su contraseña.',
  'staff.name': 'Nombre',
  'staff.email': 'Correo electrónico',
  'staff.addAndMakeCode': 'Añadir y generar un código',

  'staff.hasSecondFactor': 'tiene segundo factor',
  'staff.noSecondFactor': 'sin segundo factor',
  'staff.whatTheyHold': 'Qué tiene asignado',
  'staff.holdsNothingYet': 'Todavía nada, así que puede iniciar sesión y no verá nada.',
  'staff.everywhere': 'en todas partes',
  'staff.oneLocation': 'una sede',
  'staff.takeItAway': 'Quitárselo',
  'staff.giveARole': 'Darle un puesto',
  'staff.role': 'Puesto',
  'staff.chooseRole': 'Elija un puesto',
  'staff.holdingGrants': 'Tenerlo concede: {permissions}',
  'staff.andObligesSecondFactor': ' — y le obliga a configurar un segundo factor.',
  'staff.where': 'Dónde',
  'staff.everywhereInOrg': 'En todo el grupo',
  'staff.giveThem': 'Dárselo',
  'staff.makeNewCode': 'Generar un código nuevo',
  'staff.stopAccount': 'Bloquear esta cuenta',
  'staff.letThemBackIn': 'Volver a darle acceso',
  'staff.stoppingNote':
    'Bloquear una cuenta termina sus sesiones en la siguiente petición y no borra nada: su nombre tiene que seguir apareciendo junto al trabajo que hizo.',

  // --- Parts ----------------------------------------------------------------
  'parts.title': 'Repuestos',
  'parts.loading': 'Cargando el catálogo de repuestos…',
  'parts.denied':
    'No tiene acceso a los repuestos de esta sede. Pregunte a un responsable si cree que es un error.',
  'parts.failed': 'No se ha podido leer el catálogo.',
  'parts.actionFailed': 'No ha funcionado.',
  'parts.add': 'Añadir un repuesto',
  'parts.find': 'Buscar un repuesto',
  'parts.findPlaceholder': 'Referencia o descripción',
  'parts.findHint':
    'La referencia se busca se escriba como se escriba: MZ-690411, mz690411 y MZ 690 411 encuentran el mismo repuesto.',
  'parts.catalogueEmpty': 'Todavía no hay nada en el catálogo.',
  'parts.noMatches': 'No hay nada que coincida.',
  'parts.colNumber': 'Referencia',
  'parts.colDescription': 'Descripción',
  'parts.colWhere': 'Dónde',
  'parts.colOnHand': 'En existencias',
  'parts.colCostEach': 'Coste unidad',
  'parts.notStocked': 'Sin existencias',
  'parts.oneLocation': 'una sede',
  'parts.noneOnHand': 'Ninguno',

  'parts.costingTitle': 'Cómo se valoran los repuestos',
  'parts.costingMethod': 'Método',
  'parts.costingFutureOnly': 'solo las ventas futuras',
  'parts.costingNote':
    'El trabajo ya facturado conserva el coste al que se vendió: cambiar esto no puede rehacer un mes del que ya ha informado.',
  'parts.costingApplies': 'Esto se aplica a {futureOnly}. {rest}',

  'parts.addTitle': 'Añadir un repuesto',
  'parts.addLede':
    'Una referencia significa el mismo componente en todas las sedes, así que este es un cambio de grupo. Las existencias en sí pertenecen a la estantería en la que se den de alta.',
  'parts.partNumber': 'Referencia',
  'parts.description': 'Descripción',
  'parts.addIt': 'Añadirlo',

  'parts.noneVisible': 'Nada de esto en ninguna estantería que usted vea.',
  'parts.shelfHeading': '{code} — {quantity} en existencias a {cost} cada uno',
  'parts.deliveriesCaption': 'Entradas de {part} en {code}.',
  'parts.colReceived': 'Recibido',
  'parts.colNote': 'Nota',
  'parts.colCameIn': 'Entró',
  'parts.colLeft': 'Quedan',

  'parts.bookIn': 'Dar entrada a una recepción',
  'parts.ontoWhichShelf': 'En qué estantería',
  'parts.howMany': 'Cuántos',
  'parts.costEach': 'Coste unidad',
  'parts.deliveryNote': 'Albarán',
  'parts.bookItIn': 'Darle entrada',

  // --- This month (the dashboard) -------------------------------------------
  'dash.soFar': 'Lo que va de mes.',
  'dash.asFinished': 'El mes tal como terminó.',
  'dash.whichMonth': 'Qué mes',
  'dash.previousMonth': 'Mes anterior',
  'dash.nextMonth': 'Mes siguiente',
  'dash.previousMonthTitle': 'Mes anterior ( [ )',
  'dash.nextMonthTitle': 'Mes siguiente ( ] )',
  'dash.rooftop': 'Sede',
  'dash.everywhere': 'Todo lo que puedo ver',
  'dash.loading': 'Sumando el mes…',
  'dash.denied': 'No tiene acceso a ninguna de las cifras de este panel.',
  'dash.failed': 'No se ha podido cargar el mes.',

  'dash.withheldTrading':
    'El dinero de este mes no es suyo para verlo, así que las cifras de abajo son solo el stock.',
  'dash.withheldStock': 'El stock no es suyo para verlo, así que este mes muestra solo lo vendido.',

  'dash.booksOpen': 'La contabilidad está abierta, así que estas cifras todavía pueden moverse.',
  'dash.booksClosed': 'La contabilidad está cerrada. Estas son las cifras que se comunicaron.',
  'dash.booksClosedOn':
    'La contabilidad se cerró el {date}. Estas son las cifras que se comunicaron.',
  'dash.booksNotOpened':
    'Nadie ha abierto la contabilidad de este mes, así que no puede contabilizarse nada en él.',
  'dash.booksUnknown': 'Si la contabilidad está abierta no es algo que usted pueda ver.',

  'dash.whatTheMonthMade': 'Lo que ha dado el mes',
  'dash.totalGross': 'Margen total',
  'dash.financeShort': 'F&I',
  'dash.whatSold': 'Qué se ha vendido',
  'dash.carsDelivered': 'Vehículos entregados',
  'dash.jobsInvoiced': 'Órdenes facturadas',
  'dash.grossPerCar': 'Margen por vehículo',
  'dash.frontAndBack': 'vehículo y financiación juntos',

  'dash.whereGrossCameFrom': 'De dónde viene el margen',
  'dash.colDepartment': 'Departamento',
  'dash.colRevenue': 'Ingresos',
  'dash.colCost': 'Coste',
  'dash.colGross': 'Margen',
  'dash.colMargin': 'Margen %',
  'dash.total': 'Total',

  'dash.howOldTheStockIs': 'Antigüedad del stock',
  'dash.unsoldAsAt': ' — {count} sin vender, a {date}',
  'dash.nothingUnsold': 'No queda nada sin vender en la exposición.',
  'dash.standingLongest': 'Los que llevan más tiempo',
  'dash.colStock': 'N.º stock',
  'dash.colVehicle': 'Vehículo',
  'dash.colStatus': 'Estado',
  'dash.colDays': 'Días',
  'dash.estimatedAge':
    'No se registró fecha de adquisición, así que se cuenta desde que se dio de alta.',
  'dash.estimatedAgeNote':
    '* contado desde que se dio de alta el vehículo, porque no se registró fecha de adquisición.',

  // --- The workshop ---------------------------------------------------------
  'workshop.title': 'Taller',
  'workshop.loading': 'Cargando el taller…',
  'workshop.denied':
    'No tiene acceso al taller de esta sede. Pregunte a un responsable si cree que es un error.',
  'workshop.failed': 'No se ha podido leer la lista del taller.',
  'workshop.actionFailed': 'No ha funcionado.',
  'workshop.openOnly': 'Solo lo que sigue abierto',
  'workshop.nothingOpen': 'Ahora mismo no hay nada en el taller.',
  'workshop.empty': 'Todavía no hay órdenes aquí.',
  'workshop.caption': 'Órdenes en el taller de las sedes que usted cubre.',
  'workshop.colJob': 'Orden',
  'workshop.colCustomer': 'Cliente',
  'workshop.colVehicle': 'Vehículo',
  'workshop.colCameInFor': 'Entró por',
  'workshop.colWaiting': 'Esperando',
  'workshop.colDue': 'A pagar',
  'workshop.colStage': 'Fase',

  'workshop.waitingTitle': 'Esperando a un cliente',
  'workshop.waitingNote': {
    one: 'Una orden tiene trabajo que nadie ha aceptado pagar todavía. No puede facturarse hasta que alguien llame.',
    other:
      '{count} órdenes tienen trabajo que nadie ha aceptado pagar todavía. Ninguna puede facturarse hasta que alguien llame.',
  },
  'workshop.toAskAbout': {
    one: '{count} orden por consultar',
    other: '{count} órdenes por consultar',
  },
  'workshop.pendingNote': {
    one: 'Un trabajo está esperando al cliente. No puede cobrarse hasta que responda.',
    other:
      '{count} trabajos están esperando al cliente. Ninguno puede cobrarse hasta que responda.',
  },

  'workshop.stageBooked': 'Con cita',
  'workshop.stageInProgress': 'En curso',
  'workshop.stageCompleted': 'Trabajo terminado',
  'workshop.stageInvoiced': 'Facturada',
  'workshop.stageCancelled': 'Anulada',

  'workshop.moveInProgress': 'Empezar el trabajo',
  'workshop.moveCompleted': 'El trabajo está terminado',
  'workshop.moveInvoiced': 'Facturarla',
  'workshop.moveCancelled': 'Anular la orden',
  'workshop.moveBooked': 'Volver a con cita',

  'workshop.miles': '{count} km',
  'workshop.bookedIn': 'con cita el {date}',
  'workshop.printJobSheet': 'Imprimir la orden de trabajo',
  'workshop.printInvoice': 'Imprimir la factura',
  'workshop.whatHappened': 'Qué ha pasado',

  'workshop.theWork': 'El trabajo',
  'workshop.nothingWrittenUp': 'Todavía no se ha anotado nada.',
  'workshop.colWhat': 'Qué',
  'workshop.colDetail': 'Detalle',
  'workshop.colAgreed': '¿Aceptado?',
  'workshop.colAmount': 'Importe',
  'workshop.nobodyAsked': 'Nadie lo ha preguntado',
  'workshop.saidNo': 'Ha dicho que no',
  'workshop.agreed': 'Aceptado',
  'workshop.iRangThem': 'Le he llamado',
  'workshop.notNow': 'Ahora no',
  'workshop.howObtained': 'Cómo se obtuvo',
  'workshop.howObtainedPlaceholder': 'Llamada a las 10:40, hablé con la Sra. Okafor',
  'workshop.theySaidYes': 'Ha dicho que sí',
  'workshop.theySaidNo': 'Ha dicho que no',
  'workshop.hoursAtRate': '{hours} h a {rate}',

  'workshop.writeUpMore': 'Anotar más trabajo',
  'workshop.lineKind': 'Qué',
  'workshop.lineDescription': 'Descripción',
  'workshop.lineJob': 'Trabajo del catálogo',
  'workshop.lineJobNote':
    'Opcional. Al elegir uno se rellenan el tiempo estándar y la tarifa de esta sede; lo que usted escriba prevalece.',
  'workshop.setupLink': 'Configurar el taller',
  'workshop.clockTitle': 'Tiempo en este trabajo',
  'workshop.clockedSoFar': '{hours} horas fichadas hasta ahora. Un técnico que sigue en el trabajo no cuenta nada hasta que fiche la salida.',
  'workshop.clockWho': 'Fichar la entrada de alguien…',
  'workshop.clockOn': 'Fichar entrada',
  'workshop.clockOff': 'Fichar salida',
  'workshop.onSince': 'desde {since}',
  'workshop.someone': 'Alguien de aquí',
  'labour.hoursClocked': 'Horas fichadas',
  'labour.productivity': 'Productividad',
  'labour.colClocked': 'Fichadas',
  'labour.colProductivity': 'Productividad',
  'labour.notClocked': 'Sin fichar',

  'serviceSetup.title': 'Configuración de taller',
  'serviceSetup.loading': 'Cargando la configuración…',
  'serviceSetup.denied': 'No puede ver el taller.',
  'serviceSetup.backToWorkshop': 'Volver al taller',
  'serviceSetup.ratesTitle': 'Coste de la hora',
  'serviceSetup.ratesNote':
    'Se define por sede. Dos sedes no tienen por qué cobrar lo mismo, y la garantía se reembolsa a lo que permita el fabricante.',
  'serviceSetup.location': 'Sede',
  'serviceSetup.chooseLocation': 'Elija una sede…',
  'serviceSetup.perHour': 'Por hora',
  'serviceSetup.notSet': 'Sin definir',
  'serviceSetup.setRate': 'Fijar la tarifa',
  'serviceSetup.jobsTitle': 'Trabajos que vende el taller',
  'serviceSetup.jobsNote':
    'Compartidos por todas las sedes, como un número de pieza, para que el mismo trabajo signifique lo mismo en todas partes.',
  'serviceSetup.code': 'Código',
  'serviceSetup.describes': 'Describe',
  'serviceSetup.standardHours': 'Horas estándar',
  'serviceSetup.whoPays': 'Quién paga normalmente',
  'serviceSetup.withdraw': 'Retirar',
  'serviceSetup.restore': 'Restaurar',
  'serviceSetup.addJob': 'Añadir el trabajo',
  'serviceSetup.frozenNote':
    'Aquí no se borra nada. Retirar deja de ofrecer un trabajo y mantiene correctas todas las órdenes que ya lo citan.',
  'workshop.hours': 'Horas',
  'workshop.rate': 'Tarifa',
  'workshop.amount': 'Importe',
  'workshop.fromTheShelf': 'De la estantería',
  'workshop.notFromStock': 'No es de almacén (escríbelo abajo)',
  'workshop.howMany': 'Cuántos',
  'workshop.onTheShelf': {
    one: '{number}: {count} en la estantería.',
    other: '{number}: {count} en la estantería.',
  },
  'workshop.addLine': 'Añadirlo',

  'workshop.totalsCaption': 'A cuánto asciende la orden.',
  'workshop.totalLabour': 'Mano de obra',
  'workshop.totalParts': 'Repuestos',
  'workshop.totalSublet': 'Trabajo externo',
  'workshop.totalDue': 'A pagar',

  'workshop.whoIsOnIt': 'Quién la lleva',
  'workshop.nobodyYet': 'Todavía nadie',
  'workshop.invoicedNothingMore': 'Facturada el {date}. El trabajo está terminado; lo que se debe está abajo.',
  'workshop.jobFinished': 'Esta orden está terminada.',
  'workshop.assignedElsewhere':
    'Asignada a alguien que no está en su lista de personal: puede que trabaje en otra sede.',
  'workshop.removeLine': 'Quitar',
  'workshop.moveNote': 'Nota (queda en el expediente)',
  'workshop.howObtainedHint':
    'Esto es lo que cuenta si algún día se discute la factura. Indique con quién habló y cuándo.',
  'workshop.toAsk': {
    one: '{count} por preguntar',
    other: '{count} por preguntar',
  },
  'workshop.labourReport': 'Informe de mano de obra',

  'workshop.colWhoPays': 'Quién paga',
  'workshop.linePayType': 'Quién paga',
  'workshop.notCustomersCall': 'No le corresponde al cliente',
  'workshop.totalWarranty': 'Garantía',
  'workshop.totalInternal': 'Interno',
  'workshop.totalWork': 'Todo el trabajo',
  'workshop.writeUpNote':
    'Todo lo que se añada ahora necesita la respuesta del cliente antes de poder facturarse: de eso se trata. Anótelo mientras lo tiene delante.',
  'workshop.writeUpNoteOther':
    'Nadie tiene que llamar al cliente por esto, porque no lo paga él. Aun así queda en la orden, para que el trabajo conste y las horas se cuenten.',

  // --- Lo que vendió el taller ----------------------------------------------
  'labour.title': 'Mano de obra',
  'labour.from': 'Desde',
  'labour.to': 'Hasta',
  'labour.backToWorkshop': 'Volver al taller',
  'labour.loading': 'Calculando las cifras de mano de obra…',
  'labour.denied':
    'No tiene acceso a las cifras de este taller. Consúltelo con un responsable si cree que es un error.',

  'labour.headlineCaption':
    'Horas vendidas, ingresos por mano de obra y lo que rindió realmente una hora.',
  'labour.hoursSold': 'Horas vendidas',
  'labour.revenue': 'Ingresos por mano de obra',
  'labour.effectiveRate': 'Lo que rindió una hora',

  'labour.byTechnician': 'Por técnico',
  'labour.technicianCaption': 'Horas e ingresos de cada técnico durante el periodo.',
  'labour.colWho': 'Técnico',
  'labour.colHours': 'Horas',
  'labour.colRevenue': 'Ingresos',
  'labour.colRate': 'Por hora',
  'labour.nobodyCredited': 'Sin técnico asignado',
  'labour.notNamed': 'Sin nombre',
  'labour.namesUnavailable':
    'Aquí no se nombra a los técnicos porque usted no puede consultar la lista de personal. Las horas y los importes siguen siendo correctos.',
  'labour.nothingInvoiced':
    'No se facturó nada en este periodo, así que no hay horas que presentar.',

  'labour.byPayer': 'Quién pagó',
  'labour.payerCaption': 'Horas e ingresos, repartidos según quién liquida el trabajo.',
  'labour.colPayer': 'Pagado por',

  'labour.notMeasuredTitle': 'Lo que esto no mide',
  'labour.notMeasuredWhy':
    'Son las dos cifras por las que se suele juzgar a un taller, y ninguna de las dos puede obtenerse honestamente de lo que registra este sistema. Ambas necesitan algo que aquí nadie ha introducido nunca.',
  'labour.noEfficiency':
    'Eficiencia: horas producidas frente a horas disponibles. No hay cuadrante, así que no hay entre qué dividir.',
  'labour.noProductivity':
    'Productividad: horas facturadas frente a horas fichadas. No hay reloj de fichar, así que no hay entre qué dividir.',
  'labour.period':
    'Contado a partir del trabajo facturado entre el {from} y el {to}. El trabajo en curso no es ingreso.',

  'workshop.payTypeReport': 'Informe por forma de pago',
  'payType.title': 'Quién pagó qué',
  'payType.from': 'Desde',
  'payType.to': 'Hasta',
  'payType.loading': 'Calculando las cifras…',
  'payType.denied': 'No tiene acceso a las cifras de este taller. Consulte a un gerente si cree que es un error.',
  'payType.headlineCaption': 'Ingreso total facturado en el período.',
  'payType.totalRevenue': 'Ingreso total',
  'payType.tableCaption': 'Ingreso de mano de obra, repuestos y subcontratación, por quién paga.',
  'payType.colPayer': 'Pagado por',
  'payType.colLabour': 'Mano de obra',
  'payType.colParts': 'Repuestos',
  'payType.colSublet': 'Subcontratado',
  'payType.colRevenue': 'Total',
  'payType.colPartsCost': 'Costo de repuestos',
  'payType.colPartsGross': 'Margen de repuestos',
  'payType.colOrders': 'Órdenes',
  'payType.nothingInvoiced': 'No se facturó nada en este período, así que no hay nada que conciliar.',
  'payType.partsGrossNote':
    'El margen bruto se muestra solo para repuestos. El costo de un repuesto se fija al facturar; la mano de obra y el trabajo subcontratado no tienen un costo registrado en este sistema, así que combinar los tres en una sola cifra promediaría un número real con dos inventados.',

  // --- Llamadas a revisión ---------------------------------------------------
  'recalls.title': 'Llamadas a revisión',
  'recalls.onRequest':
    'Esto consulta al organismo de seguridad vial, así que solo se ejecuta cuando usted lo pide.',
  'recalls.check': 'Buscar llamadas a revisión',
  'recalls.checkAgain': 'Buscar otra vez',
  'recalls.checking': 'Consultando al organismo…',
  'recalls.caveat':
    'Estas son las campañas publicadas para un {make} {model} de {year}. El registro se lleva por modelo y no por vehículo: no dice si a este se le ha hecho el trabajo; eso solo lo sabe el fabricante.',
  'recalls.noneFound':
    'No hay campañas publicadas para este modelo. Eso no es lo mismo que haber revisado este coche.',
  'recalls.doNotDrive': 'No conducir',
  'recalls.parkOutside': 'Aparcar al aire libre',
  'recalls.remedy': 'Solución: {remedy}',

  // --- Claves de acceso ------------------------------------------------------
  'passkey.useOne': 'Usar una clave de acceso',
  'passkey.needDealerGroup':
    'Escriba primero su grupo: es lo que determina en qué concesionario entra.',
  'passkey.ceremonyFailed':
    'Su dispositivo no pudo completarlo. Inténtelo de nuevo o entre con su contraseña.',

  'passkey.title': 'Claves de acceso',
  'passkey.lede':
    'Una clave de acceso le identifica con el teléfono u ordenador que ya desbloquea, en lugar de con una contraseña. Su contraseña sigue funcionando y nada de esta pantalla se la quita.',
  'passkey.addTitle': 'Añadir una clave de acceso',
  'passkey.addNote':
    'Su dispositivo le pedirá confirmación. De él no sale nada secreto: solo una clave pública, que no sirve de nada a quien se lleve una copia.',
  'passkey.label': 'Cómo llamarla',
  'passkey.labelPlaceholder': 'Portátil del trabajo',
  'passkey.labelHint':
    'Verá este nombre cuando vaya a quitarla, así que nombre el dispositivo y no a usted mismo.',
  'passkey.add': 'Añadirla',
  'passkey.adding': 'Esperando a su dispositivo…',
  'passkey.added': '{label} queda registrada.',
  'passkey.unsupported':
    'Este navegador no puede usar claves de acceso. La mayoría sí puede, en una conexión que no sea http simple.',

  'passkey.yoursTitle': 'Sus claves de acceso',
  'passkey.loading': 'Cargando sus claves de acceso…',
  'passkey.none': 'Todavía no tiene ninguna clave de acceso.',
  'passkey.caption': {
    one: '{count} clave de acceso en esta cuenta',
    other: '{count} claves de acceso en esta cuenta',
  },
  'passkey.colLabel': 'Nombre',
  'passkey.colAdded': 'Añadida',
  'passkey.colLastUsed': 'Último uso',
  'passkey.neverUsed': 'Nunca usada',
  'passkey.forget': 'Olvidarla',
  'passkey.forgetTitle': '¿Olvidar {label}?',
  'passkey.forgetConfirm':
    'Ese dispositivo dejará de poder identificarle, y esto no se puede deshacer.',
  'passkey.forgot': '{label} ya no está.',

  // --- Getting back into an account -----------------------------------------
  'recover.link': 'He olvidado mi contraseña',
  'recover.title': 'Volver a entrar',
  'recover.lede':
    'Elija cómo puede demostrar que la cuenta es suya. Use el que use, establecerá aquí mismo una contraseña nueva.',
  'recover.withAuthenticator': 'Usar mi aplicación de autenticación',
  'recover.withAuthenticatorHint':
    'Para quien tenga configurada la verificación en dos pasos. Un código de la aplicación, o uno de los códigos de recuperación que guardó.',
  'recover.withCode': 'Usar un código de mi responsable',
  'recover.withCodeHint':
    'Pida a un responsable que emita uno desde la pantalla de Personas. Se lo lee en voz alta; dura cuatro horas.',
  'recover.email': 'Correo electrónico',
  'recover.codeFromApp': 'Código de su aplicación de autenticación',
  'recover.codeFromManager': 'El código que le ha dado su responsable',
  'recover.newPassword': 'Nueva contraseña',
  'recover.newPasswordAgain': 'Repita la nueva contraseña',
  'recover.mismatch': 'Las dos no coinciden.',
  'recover.submit': 'Establecer mi contraseña',
  'recover.working': 'Estableciéndola…',
  'recover.doneTitle': 'Ya está',
  'recover.doneLede':
    'Su contraseña ha cambiado y se ha cerrado la sesión en todos los dispositivos. Vuelva a iniciar sesión con la nueva.',
  'recover.toSignIn': 'Ir a iniciar sesión',
  'recover.back': 'Elegir otra forma',
  'recover.noMethods':
    'Esta instalación no tiene ninguna forma de recuperar una cuenta por sí sola. Pida a un responsable que vuelva a darle de alta.',

  // --- Handing out a reset (the People screen) ------------------------------
  'staff.resetTitle': 'Restablecer su contraseña',
  'staff.reset': 'Emitir un código de restablecimiento',
  'staff.resetting': 'Emitiendo…',
  'staff.resetNote':
    'Esto le permite volver a iniciar sesión como sí mismo. Lea el código en voz alta: se muestra una sola vez y dura cuatro horas.',
  'staff.resetWarning':
    'Está entregando la capacidad de iniciar sesión como esta persona. Asegúrese de que es con ella con quien habla.',
  'staff.resetIssued': 'Se emitió un código de restablecimiento {when} y todavía no se ha usado.',
  'staff.resetDone': 'Ya se lo he leído',

  'deals.awaitingTitle': 'Esperando a un responsable',
  'deals.awaitingNote': {
    one: '{count} operación todavía no la ha aprobado nadie, y no puede entregarse hasta que lo esté.',
    other:
      '{count} operaciones todavía no las ha aprobado nadie, y no pueden entregarse hasta que lo estén.',
  },
  'inline.changeThis': '{label}: {value}. Pulse para cambiarlo.',
  'inline.saved': 'Guardado',

  // --- The service diary ----------------------------------------------------
  'enum.appointmentStatus.Scheduled': 'Prevista',
  'enum.appointmentStatus.Arrived': 'Ha llegado',
  'enum.appointmentStatus.NoShow': 'No vino',
  'enum.appointmentStatus.Cancelled': 'Anulada',

  'diary.title': 'Próximas entradas',
  'diary.loading': 'Cargando la agenda…',
  'diary.empty': 'No hay nada citado. La agenda está libre.',
  'diary.count': {
    one: '{count} vehículo previsto',
    other: '{count} vehículos previstos',
  },
  'diary.dayLoad': {
    one: '{count} vehículo, {hours} h de trabajo',
    other: '{count} vehículos, {hours} h de trabajo',
  },
  'diary.dayLoadSome': {
    one: '{count} vehículo, {hours} h citadas y {unestimated} sin estimar',
    other: '{count} vehículos, {hours} h citadas y {unestimated} sin estimar',
  },
  'diary.unestimated': 'Sin estimar',
  'diary.colWhen': 'Cuándo',
  'diary.colCustomer': 'Cliente',
  'diary.colVehicle': 'Vehículo',
  'diary.colReason': 'Para qué',
  'diary.colHours': 'Est.',
  'diary.colWhat': 'Qué hacer',
  'diary.itsHere': 'Ya está aquí',
  'diary.arriving': 'Abriendo la orden…',
  'diary.didNotCome': 'No vino',
  'diary.becameJob': 'Orden {number}',

  'diary.book': 'Citar un vehículo',
  'diary.bookTitle': 'Citar un vehículo',
  'diary.customer': 'Cliente',
  'diary.vehicle': 'Vehículo',
  'diary.when': 'Cuándo',
  'diary.hours': 'Horas de trabajo previstas',
  'diary.hoursHint': 'Déjelo en blanco si nadie lo ha estimado todavía.',
  'diary.reason': 'Para qué lo trae',
  'diary.reasonPlaceholder': 'Mantenimiento anual',
  'diary.take': 'Citarlo',
  'diary.taking': 'Citando…',
  'diary.pickCustomer': 'Elija un cliente',
  'diary.pickVehicle': 'Elija un vehículo',
  'diary.pickCustomerFirst': 'Elija primero el cliente; después se ofrecen sus coches.',

  // --- The control-plane console --------------------------------------------
  // A separate vocabulary from the dealership's, deliberately: whoever reads
  // these screens runs the installation and never sees a vehicle. "Concesionario"
  // here means an account on a server, not a place with a forecourt.
  'admin.badge': 'Administración',
  'admin.navDealerships': 'Concesionarios',
  'admin.navSupportAccess': 'Acceso de soporte',

  'admin.signInLede': 'Esto le identifica en la instalación, no en un concesionario.',
  'admin.signInCode': 'Código de su aplicación de autenticación',
  'admin.signInCodeNote': 'Déjelo en blanco solo si todavía no ha configurado ninguna.',

  'admin.secondFactorTitle': 'Configure su segundo factor',
  'admin.secondFactorRequired':
    'Las cuentas de administrador tienen que tener uno. Hasta que lo configure, esta es la única pantalla que puede usar.',
  'admin.secondFactorIntro':
    'Esta cuenta puede entrar en cualquier concesionario de esta instalación, así que una contraseña por sí sola no basta para protegerla.',
  'admin.noRecoveryCodes':
    'No hay códigos de recuperación para una cuenta de administrador. Si pierde este móvil, alguien con acceso a la base de datos tendrá que restablecerlo por usted.',

  'admin.dealerships': 'Concesionarios',
  'admin.setUpDealership': 'Dar de alta un concesionario',
  'admin.suspendConfirm':
    'Todo su personal se desconecta inmediatamente y no podrá trabajar hasta que se reanude.',
  'admin.dealershipReady': '{name} está listo',
  'admin.firstManager':
    'Su primer responsable es {email}. Léale este código: con él establece su propia contraseña en la pantalla de inicio de sesión.',
  'admin.codeShownOnce': 'Esta es la única vez que puede mostrarse.',
  'admin.codeShownOnceWhy':
    'Solo se guarda una copia cifrada, así que no puede volver a consultarse. Si se pierde, se le puede emitir uno nuevo desde la pantalla de Personas del propio concesionario. La contabilidad está abierta, así que pueden operar de inmediato.',
  'admin.passedItOn': 'Ya se lo he dado',
  'admin.loadingDealerships': 'Cargando la lista de concesionarios…',
  'admin.noDealerships':
    'Todavía no hay concesionarios en esta instalación. Dé de alta el primero arriba.',
  'admin.dealershipsCaption': {
    one: '{count} concesionario en esta instalación',
    other: '{count} concesionarios en esta instalación',
  },
  'admin.colName': 'Nombre',
  'admin.colKey': 'Clave',
  'admin.colStatus': 'Estado',
  'admin.colSchema': 'Esquema',
  'admin.colInService': 'En servicio',
  'admin.resume': 'Reanudar',
  'admin.suspend': 'Suspender',
  'admin.suspendTitle': '¿Suspender {name}?',

  'admin.setUpNote':
    'Esto crea su base de datos, abre su contabilidad de este mes y crea un responsable que después añade a todos los demás. Usted nunca verá ni elegirá su contraseña.',
  'admin.dealershipName': 'Nombre del concesionario',
  'admin.shortName': 'Nombre corto',
  'admin.shortNameHint':
    'Letras minúsculas, dígitos y guiones. Su personal lo escribe para iniciar sesión y no puede cambiarse después.',
  'admin.firstLocation': 'Primera sede',
  'admin.firstLocationPlaceholder': 'Sede principal',
  'admin.locationCode': 'Código de sede',
  'admin.managerName': 'Nombre del responsable',
  'admin.managerEmail': 'Correo del responsable',
  'admin.setItUp': 'Darlo de alta',
  'admin.settingItUp': 'Dándolo de alta…',
  'admin.notCreated': 'El concesionario no se ha creado.',

  'admin.supportAccess': 'Acceso de soporte',
  'admin.supportEnter': 'Entrar en un concesionario',
  'admin.supportLede':
    'Podrá leer sus registros y no cambiar nada, durante una hora como máximo. Ellos lo ven en su propio registro, con su nombre y el motivo que indique aquí.',
  'admin.supportDealership': 'Concesionario',
  'admin.supportReason': 'Por qué necesita entrar',
  'admin.supportReasonNote':
    'Esto queda registrado de forma permanente, tanto en su registro como en el nuestro. Escriba algo que no le importaría que leyeran.',
  'admin.supportOpen': 'Abrir el acceso',
  'admin.supportOpening': 'Abriendo…',
  'admin.supportOpened':
    'Está dentro de {tenant} hasta las {time}. Abra las pantallas del concesionario en este navegador para mirar; cierre la visita abajo cuando termine.',
  'admin.supportRecord': 'El registro',
  'admin.supportLoading': 'Cargando el registro…',
  'admin.supportEmpty': 'Todavía nadie ha entrado en un concesionario.',
  'admin.supportCaption': {
    one: '{count} visita de soporte',
    other: '{count} visitas de soporte, de la más reciente a la más antigua',
  },
  'admin.supportColWho': 'Quién',
  'admin.supportColWhy': 'Por qué',
  'admin.supportColOpened': 'Abierta',
  'admin.supportColState': 'Estado',
  'admin.supportCloseNow': 'Cerrar ahora',
  'admin.supportExpired': 'Caducada',
  'admin.supportClosed': 'Cerrada el {date}',

  'nav.ageing': 'Quién nos debe',
  'nav.statements': 'Estados de cuenta',

  'ageing.title': 'Quién nos debe',
  'ageing.loading': 'Calculando quién debe qué…',
  'ageing.denied': 'No tiene acceso a esto.',
  'ageing.failed': 'No se pudo cargar el informe de antigüedad de saldos.',
  'ageing.empty': 'Nadie nos debe nada en este momento.',
  'ageing.colCustomer': 'Cliente',
  'ageing.colCurrent': 'Al día',
  'ageing.col31to60': '31–60 días',
  'ageing.col61to90': '61–90 días',
  'ageing.colOver90': 'Más de 90 días',
  'ageing.colTotal': 'Total',
  'ageing.totals': 'Total',

  'statement.title': 'Estado de cuenta del cliente',
  'statement.customer': 'Cliente',
  'statement.from': 'Desde',
  'statement.to': 'Hasta',
  'statement.pickCustomer': 'Elija un cliente para ver su estado de cuenta.',
  'statement.loading': 'Calculando el estado de cuenta…',
  'statement.denied': 'No tiene acceso a esto.',
  'statement.failed': 'No se pudo cargar el estado de cuenta.',
  'statement.opening': 'Saldo anterior',
  'statement.closing': 'Saldo adeudado',
  'statement.colDate': 'Fecha',
  'statement.colReference': 'Referencia',
  'statement.colAmount': 'Importe',
  'statement.colBalance': 'Saldo',
  'statement.kindInvoice': 'Facturado',
  'statement.kindPayment': 'Pagado',
  'statement.empty': 'No ocurrió nada en este período.',

  'customers.creditLimit': 'Límite de crédito',
  'customers.creditLimitNone': 'Sin límite establecido',
  'customers.creditLimitEdit': 'Cambiar',
  'customers.creditLimitPlaceholder': 'Sin límite',
  'customers.creditLimitSave': 'Guardar',
  'customers.creditLimitSaving': 'Guardando…',
};
