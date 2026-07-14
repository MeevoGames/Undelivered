# Dicegeon — Modo Torneo de Dados

> Documento de diseño. Fuente de verdad del modo noche.
> El plan de implementación por fases vive en [Dicegeon-Plan.md](Dicegeon-Plan.md).

## Concepto general

Dicegeon es el segundo gran modo del juego. Mientras que durante el día el jugador trabaja
clasificando paquetes para conseguir **oro, gemas, dados y efectos**, durante la noche participa en
torneos de dados donde pone a prueba la colección que fue construyendo.

**No es un roguelike donde todo se reinicia.** La progresión es permanente: el jugador va ampliando
su colección de dados y efectos combate tras combate. El objetivo final es avanzar por torneos cada
vez más difíciles hasta conquistar el campeonato mundial.

---

## Flujo completo

### 1. Selección de torneo

Al comenzar la noche se muestran los torneos disponibles.

- **Inicio del juego:** solo aparece un torneo. El jugador únicamente puede entrar a ese. Sirve como
  tutorial.
- **Juego avanzado:** se muestran entre 2 y 3 torneos distintos.

Cada torneo informa: **nombre, dificultad, recompensas, reglas especiales, cantidad de combates y
jefe final** (si se conoce).

Ejemplos:

**Copa del Hierro**
- Recompensas: 2 Gemas · Dado Raro
- Reglas: los valores 5 y 6 hacen 50% menos daño · los enemigos tienen +10% vida

**Torneo Arcano**
- Recompensas: Efecto Épico · Oro
- Reglas: todos los efectos duran un turno adicional

El jugador elige el torneo que mejor se adapte a su colección.

### 2. Presentación del combate

Antes de cada combate el jugador puede ver toda la información necesaria: **rival, vida de cada pieza
enemiga, orden de aparición, habilidades de cada pieza y reglas activas del torneo.**

El jugador sabe exactamente contra qué va a jugar. **No existe información oculta.**

### 3. Preparación

**Elegir dados.** El jugador posee una colección permanente de dados, pero solo puede equipar una
cantidad limitada. Comienza con **2 dados** y mediante mejoras permanentes puede llegar hasta **6**.
El **orden elegido es importante** y puede reorganizarse antes del combate.

**Elegir efectos.** También equipa efectos consumibles. Los efectos solo pueden usarse si fueron
equipados previamente. Una vez utilizados desaparecen durante el resto del combate. Si además se
consumen para el resto del torneo o se recuperan entre combates queda como **decisión de balance
pendiente**.

### 4. Tienda

Antes de iniciar **cada** combate existe una tienda. Siempre aparecen **3 objetos**, generados con
estas reglas:

- exactamente **1 dado individual**
- al menos **1 efecto**
- el tercer objeto puede ser cualquiera

Ejemplos: `Dado de Cristal · Duplicar dado · Dado Pesado` — o — `Dado Vampiro · Curación · Cambiar orden de turnos`

**Relanzar tienda:** el jugador puede gastar gemas para volver a generar los tres objetos. Esto
permite buscar piezas para completar una build.

**Composición de la pantalla de tienda**

1. Los **3 objetos** aleatorios.
2. Botón para **relanzar** la tienda (cuesta gemas).
3. Botón para **mejorar tu build**: sube la cantidad de dados que podés usar (`MaxDiceSlots`).
4. Arriba a la izquierda, tu cantidad de **gemas**. Al tocar ese número se abre la pestaña de
   **trueque oro → gemas**.

### 5. Inicio del combate

Cada combate enfrenta al jugador contra entre **2 y 5 piezas enemigas**. La cantidad depende del
avance del torneo. Cada pieza tiene **vida, habilidades y posición**. No todas hacen simplemente daño.

#### Tipos de habilidades enemigas

**Pasivas permanentes** — funcionan mientras la pieza siga viva.
> Todos los enemigos hacen +1 daño · el jugador recibe menos curación · los críticos hacen menos daño

**Por rango de vida** — se activan cuando la vida baja cierto porcentaje.
> Al llegar al 50%: obtiene escudo · gana un turno extra · aumenta daño

**Reactivas** — responden a las acciones del jugador.
> Si usás un efecto · si lanzás un mini dado · si sale un 6 · si duplicás un dado · si usás tres efectos

**Al morir** — generan consecuencias.
> Explotan · curan aliados · invocan otra pieza · dejan una maldición · fortalecen al jefe

### 6. Desarrollo del combate

El combate es completamente **por turnos**.

**Turno del jugador.** Debe seleccionar uno de los dados **disponibles** y lanzarlo. Su resultado
depende de sus seis caras; cada dado posee una distribución diferente. Después del lanzamiento el
jugador puede (si corresponde) aplicar efectos: transformar dados, crear mini dados, cambiar orden,
curar, duplicar resultados. Todo depende de los efectos equipados.

Al finalizar su acción, **el dado queda agotado**: no puede volver a utilizarse hasta completar el ciclo.

**Turno enemigo.** El enemigo utiliza **una** de sus piezas. Cada una puede atacar, aplicar estados,
invocar, curar o activar habilidades.

**Ciclo de dados.** Cuando todos los dados del jugador fueron utilizados, **todos vuelven
automáticamente a estar disponibles** y comienza un nuevo ciclo. Esto continúa hasta que alguien gane.

---

## Dados

Una cara **solo puede estar vacía o tener un número**. No existen caras de efecto: los efectos son
items aparte. Tampoco hay tipos de daño.

Cada dado posee **personalidad propia**. No solamente cambia el daño. Puede tener caras vacías,
números repetidos, un número extremadamente alto, números bajos constantes, distribuciones muy
riesgosas o muy estables.

```
1 1 1 6 6 6        0 0 0 10 10 10        3 3 3 3 3 3
```

No existen dos dados necesariamente iguales. El jugador construye su colección buscando sinergias.

## Efectos

Los efectos son items que modifican una tirada o el combate. Pueden durar **solo ese turno**,
**varios turnos** o **todo el combate**; su momento de uso es tan importante como el efecto.

### Tipos de efecto

1. **Afectar el número de la tirada** — sumar X al resultado, o multiplicarlo ×X.
2. **Convertir el valor en otra stat** — por defecto la tirada es **daño**; ciertos efectos la
   convierten en **cura / escudo / velocidad**.
3. **Otorgar turnos** — duplicar turnos, o relanzar el mismo dado 2 veces.
4. **Transformar números** — regla condicional sobre el resultado: se compara con `mayor/menor/igual
   a X`; si se cumple, se convierte en `Y`. Ej: «si el resultado es menor a 3 → se convierte en 9».
5. **Bloqueo de números** — si el resultado **del oponente** es `mayor/igual a X` → se convierte en 0.
6. **Manipulación de turnos** — «me adelanto» (juego primero aunque no sea mi turno), o «tomo al más
   rápido y lo mando último» en el orden de turnos.
7. **Renovación** — renueva tu deck de **dados** o de **efectos**; o renueva un dado/efecto puntual
   luego de usarse.
8. **Crear nuevos dados** — tras tu tirada crea **X mini dados** con valores aleatorios (X = el
   resultado) y los lanza; su suma va al daño. Ej: sacás 3 → 3 mini dados (`1/2/3`) que se lanzan.
9. **Contraataque** — el dado lanzado se gasta **sin causar daño**, pero el siguiente ataque que
   recibas se le **devuelve** al enemigo que lo lanzó.
10. **Jugar con sangre** — perdés 1 de vida por ronda, pero al valor del dado se le **suma tu vida
    actual** como daño al oponente.
11. **Saltarse escudo** — el daño va **directo a la vida**, ignorando el escudo enemigo.
12. **Degradar el dado enemigo** — por un turno, el dado del enemigo se convierte en caras `1-2-3`.
13. **Sumar un estado al daño** — el dado lleva un estado (**Quema / Veneno / Congelamiento**); el
    enemigo atacado recibe **el resultado del dado como marcas** de ese estado (ver Estados).
14. **Modificación de suerte** — suma/multiplica la **suerte** actual durante un turno (ver Suerte).

### Rarezas

El **mismo efecto** existe en 3 rarezas, que cambian su economía de uso:

| Rareza | Al usarlo | Al terminar el torneo |
| --- | --- | --- |
| Común | queda gastado el resto del **combate** | se destruye |
| Rara | queda gastado el resto del **torneo** | se destruye |
| Épica | queda gastado el resto del **torneo** | **vuelve** al inventario |

> «No se consume, solo se gasta»: la épica no puede volver a usarse en ese torneo, pero para el
> siguiente sigue en tu inventario.

## Mini dados

Algunos efectos generan mini dados:

> Si un dado obtiene un 5 → se crea automáticamente un mini dado con valores `1 2 3` → se lanza
> inmediatamente.

Esto puede iniciar **cadenas enormes de efectos**.

## Estados

Marcas aplicadas a un personaje que actúan con el tiempo:

- **Quema** — 2 de daño por cada **turno de ese enemigo**.
- **Veneno** — 1 de daño por cada **turno que pasa** (de cualquier personaje).
- **Congelamiento** — reduce en **1** el resultado del dado que lanza ese enemigo.

Cada **marca** de un estado **duplica** su valor. Ej: 3 marcas de quema → **6** de daño por cada turno
de ese enemigo.

## Suerte

Stat **todavía no creada**. En el futuro el jugador podrá maximizarla; afectará la probabilidad de
sacar **números altos** en una tirada. Los efectos de «modificación de suerte» (tipo 14) la
suman/multiplican por un turno.

## Builds

El objetivo del jugador es construir una estrategia. No existe una única build dominante; con los
dados, efectos y estados se busca que el jugador quiera **rejugar** probando builds como:

- **Mini dados** — creación → multiplicación → modificación de resultados → estados.
- **Control** — congelamiento → degradación de dados → manipulación de turnos.
- **Velocidad** — convertir tiradas en velocidad → adelantarse → turnos extra → renovar dados.
- **Sangre** — curación → vida alta → sacrificar vida → convertir vida en daño.
- **Estados** — muchos dados pequeños → aplicar muchas marcas → acelerar turnos para disparar estados.
- **Suerte** — bonificadores de suerte → renovación de efectos → renovación de dados → multiplicadores.

Las builds evolucionan constantemente según la colección.

## Torneos

Cada torneo **modifica las reglas del juego**: los valores altos hacen menos daño, los efectos duran
más, los enemigos tienen más vida, usar ciertos efectos fortalece enemigos, los mini dados hacen
doble daño, las curaciones están reducidas.

Esto obliga al jugador a adaptar su build. No siempre conviene usar la estrategia más poderosa.

## Recompensas

Al ganar combates o torneos: **oro, gemas, dados nuevos, efectos nuevos, objetos raros y mejoras
permanentes**. Expanden la colección y habilitan nuevas combinaciones para futuros torneos.

## Filosofía de diseño

Dicegeon no busca que el jugador gane por azar. El dado introduce incertidumbre, pero el núcleo
estratégico está en las **decisiones previas** y la **gestión del combate**: la elección del torneo,
la composición del set de dados, la selección y el momento de uso de los efectos, y el orden en que
se enfrentan las piezas enemigas.

El jugador no controla el resultado de cada tirada, pero sí controla casi todo lo que la rodea.

---

## Estado del código actual frente a este diseño

### Se conserva
- `ModeSwitcher` (transición día→noche, trigger `Change`).
- `DiceData` como dado de 6 caras.
- `DieView`, `RollZone`, `DieRoller` / `DieRoller2D` (arrastrar → rodar → cara).
- Gemas en `StatsManager` y el **trueque oro→gemas**.
- La idea de loadout de `InventoryWindow` (modo "seleccionar dados").
- El **sistema de cajas** (`BoxData`, `BoxOpener`, `BoxPickPanel`, `BoxView`) — se generaliza.

### Se rehace
- **`DiceFaceData`** → una cara es **vacía o un número**. Se van `EffectKind` (daño/defensa/cura) y
  `DamageType`.
- **`Inventory`** → colección permanente (dados + efectos + cajas) + **loadout ordenado** de 2 a 6
  dados (hoy es un set fijo de 4 sin orden).
- **Cajas** → de **dados** y de **efectos**, cada una en dos formatos: **elegí 1 de 3** y
  **1 al azar**. (`DiceBoxData` se generaliza; `UpgradeBoxData` pasa a ser caja de efectos.)
- **`TournamentData`** → nombre, dificultad, recompensas, reglas, cantidad de combates, jefe.
- **`EnemyData`** → habilidades con disparadores (pasiva / umbral de vida / reactiva / al morir).
- **`TournamentManager`** / `CombatScreenUI` → motor de combate por turnos con ciclo de dados.

### Se retira
- La tienda de 4 tabs: `ShopWindow` (tabs), `DiceShopTab`, `DiceBoxShopTab`, `UpgradeBoxShopTab`,
  `NightUpgradeShopTab`. La reemplaza la **tienda de 3 objetos** pre-combate.
- `NightFeatureUpgradeData` y los multiplicadores (`Multiplicador`, `MultiplicadorSuerte`,
  `MultiplicadorActivable`) — los reemplazan los **efectos**.
- `DamageType` y las caras `Defensa1..6`, `Cura1..6`, `Luna`, `Mecha`, `Reptil`.
- La reempaquetadora nocturna / fabricador (no aparece en este diseño).

---

## Decisiones tomadas

1. **Caras.** Una cara es **vacía o un número**. No hay caras de defensa/curación ni tipos de daño;
   los efectos son items.
2. **Efectos.** Tres rarezas (ver tabla arriba). Común: gastado el resto del combate, se destruye al
   final del torneo. Rara: gastado el resto del torneo, se destruye. Épica: gastado el resto del
   torneo, **vuelve** para el siguiente.
3. **Moneda.** Sobrevive el trueque **oro → gemas**. En torneos avanzados se puede ganar **oro
   directamente**, para cambiarlo por gemas. Narrativamente, ganás el juego cuando podés dejar el
   trabajo de día y dedicarte a los dados.
4. **Orden de turnos.** Alterna **jugador vs enemigos**. "Cambiar orden de turnos" define quién juega
   primero, no reordena piezas enemigas.
5. **Loadout.** Arranca en **2**, techo base **6** (mejoras permanentes). Ciertos **efectos
   especiales** permiten superar el 6 puntualmente.
6. **Recompensas del día.** Las gift-cards pasan a ser **efectos, dados y cajas**.

7. **Tienda.** 3 objetos + botón de relanzar + botón de mejorar build (`MaxDiceSlots`) + contador de
   gemas arriba a la izquierda que abre la pestaña de trueque oro→gemas.

### Pendiente de confirmar
- La tabla de rarezas de arriba es mi lectura de «se consume por combate / por torneo / no se
  consume». Confirmar antes de la Fase 3.
