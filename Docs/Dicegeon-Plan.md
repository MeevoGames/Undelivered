# Dicegeon — Plan de trabajo

Diseño de referencia: [Dicegeon.md](Dicegeon.md)

Cada fase se cierra **solo** cuando pasa su validación. El orden está elegido para que el motor de
combate exista temprano: todo lo demás (efectos, habilidades, reglas, tienda) se engancha en él.

---

## Fase 0 — Reestructura y cimientos

**Qué se hace**
- `DiceFaceData` → **vacía o número** (se van `EffectKind` y `DamageType` y sus assets).
- Retirar la tienda de 4 tabs y `NightFeatureUpgradeData` + multiplicadores. **Las cajas se conservan**
  y se generalizan (dados / efectos, en formato "elegí 1 de 3" y "1 al azar").
- Partir `Inventory` en dos: **Colección** permanente (dados + efectos + cajas) y **Loadout** (dados
  equipados **ordenados**, de 2 a 6; efectos equipados).
- `MaxDiceSlots` como stat permanente, arranca en **2**, techo base 6 (superable por efectos).
- `EffectData` (ScriptableObject) — esqueleto: tipo, duración, **rareza** (común / rara / épica).

**Cómo se valida**
- El proyecto compila y la escena de noche abre sin referencias huérfanas.
- El inventario lista dados, efectos y cajas por separado.
- El loadout acepta como máximo `MaxDiceSlots` y **respeta el orden**: reordenar cambia la lista.
- Los 3 dados de ejemplo (`1 1 1 6 6 6`, `0 0 0 10 10 10`, `3 3 3 3 3 3`) existen y, con 1000 tiradas,
  solo salen esas caras y con la frecuencia esperada.

---

## Fase 1 — Motor de combate (el corazón)

**Qué se hace**
- `CombatManager` con máquina de estados: `Presentación → TurnoJugador → Resolución → TurnoEnemigo → … → Victoria/Derrota`.
- Dados **disponibles / agotados**. Ciclo: cuando se agotan todos, todos vuelven a estar disponibles.
- De 2 a 5 piezas enemigas con vida, posición y orden; ataque básico.
- Vida del jugador. Victoria al eliminar todas las piezas; derrota al llegar a 0.
- Sin efectos, sin habilidades, sin reglas de torneo todavía.

**Cómo se valida**
- Se puede **ganar** y **perder** un combate, contra 2 y contra 5 enemigos.
- Con 2 dados equipados: al usar el segundo, ambos vuelven a estar disponibles (ciclo visible en pantalla).
- Un dado agotado **no** se puede seleccionar.
- El enemigo actúa exactamente **una pieza por turno**, en el orden mostrado.

---

## Fase 2 — Presentación y preparación

**Qué se hace**
- Pantalla de presentación del combate: rival, vida de cada pieza, orden de aparición, habilidades,
  reglas activas.
- Pantalla de preparación: equipar y **reordenar** dados, equipar efectos.

**Cómo se valida**
- Lo que muestra la presentación coincide **1:1** con lo que ocurre en combate (mismo origen de datos;
  no hay información oculta).
- Reordenar los dados cambia el orden con el que aparecen en combate.
- Un efecto **no equipado** no puede usarse durante el combate.

---

## Fase 3 — Sistema de efectos

**Qué se hace**
- `EffectData` + resolución: duraciones (este turno / N turnos / todo el combate) y consumo al usar.
- Tipos: duplicar y triplicar dado, transformar dado en otro, cambiar orden de turnos, turno extra,
  convertir ataque en curación, alterar el resultado de una tirada, cambiar un dado en combate.

**Cómo se valida**
- Cada tipo de efecto tiene su caso de prueba: se aplica, se consume y **desaparece del set equipado**
  durante el resto del combate.
- Las duraciones bajan un turno por turno y expiran exactamente cuando corresponde.
- Un "turno extra" produce **exactamente uno** (no encadena solo).

---

## Fase 4 — Mini dados y cadenas

**Qué se hace**
- Generación de mini dados por disparador (p. ej. "si sale un 5"), con su propia cara-lista (`1 2 3`).
- Se lanzan **inmediatamente**; sus resultados pueden disparar más efectos (cadenas).
- Tope de profundidad para cortar recursión.

**Cómo se valida**
- Un dado con disparador crea el mini dado, lo tira solo, y su resultado se aplica.
- Una cadena de N eslabones se resuelve **en orden** y **termina**; con un caso construido a propósito
  para encadenarse infinito, corta en el tope y no cuelga.

---

## Fase 5 — Habilidades enemigas

**Qué se hace**
- Sistema de disparadores:
  - **Pasiva permanente** (mientras la pieza viva)
  - **Por rango de vida** (umbral %)
  - **Reactiva** (usar un efecto, lanzar un mini dado, sacar un 6, duplicar un dado, usar tres efectos)
  - **Al morir** (explotar, curar aliados, invocar, maldición, fortalecer al jefe)

**Cómo se valida**
- Cada disparador se activa exactamente cuando debe; los de umbral, **una sola vez**.
- Al morir una pieza con pasiva, su pasiva **deja de aplicarse** de inmediato.
- "Invocar" agrega una pieza al combate y esa pieza **entra al orden de turnos**.

---

## Fase 6 — Torneos y reglas

**Qué se hace**
- `TournamentData`: nombre, dificultad, recompensas, reglas, cantidad de combates, jefe.
- Sistema de reglas que engancha en daño, duración de efectos, vida enemiga, curación y mini dados.

**Cómo se valida**
- Con la regla "los valores 5 y 6 hacen 50% menos daño", un 6 hace exactamente la mitad.
- Con "los efectos duran un turno adicional", una duración de 2 pasa a 3.
- Las reglas se **muestran** en la presentación y se **aplican** en combate, leyendo el mismo dato.

---

## Fase 7 — Selección de torneo y tienda pre-combate

**Qué se hace**
- Pantalla de selección: 1 torneo al inicio (tutorial), 2-3 después, con su ficha completa.
- Tienda antes de **cada** combate: 3 objetos (exactamente 1 dado, al menos 1 efecto, el tercero
  libre) y **relanzar** gastando gemas.

**Cómo se valida**
- Sobre 100 generaciones seguidas: **siempre** hay exactamente 1 dado y **al menos** 1 efecto.
- Relanzar descuenta gemas y cambia los tres objetos.
- Comprar agrega el objeto a la **colección permanente** (sigue estando en el combate siguiente).

---

## Fase 8 — Recompensas, progresión y bucle día↔noche

**Qué se hace**
- Recompensas por combate y por torneo: oro, gemas, dados, efectos, objetos raros, mejoras permanentes
  (p. ej. subir los slots de dados de 2 a 6).
- Persistencia de la colección. Volver al trabajo de día al ganar el torneo o al morir.

**Cómo se valida**
- Ganar un torneo otorga **exactamente** las recompensas que su ficha anunciaba.
- Una mejora permanente de slots sube el techo del loadout y **persiste** a la noche siguiente.
- Bucle completo: día → noche → torneo → día, con la colección intacta.

---

## Transversal — Balance

Se hace al final de cada fase y a fondo tras la Fase 8: distribuciones de dados, vida enemiga, costos
en gemas, potencia de efectos y dureza de las reglas de torneo.
