# Sistema de escucha de "El Ciego"

**Proyecto:** Noiseless
**Subsistema:** Detección auditiva del enemigo ("El Ciego")
**Motor:** Unity 6000.6.9f1 (URP, Input System)
**Estado:** Diseño acordado, pendiente de implementación
**Versión del documento:** 1.1 (15/11/2026)

> Documento de diseño técnico del subsistema de escucha. Sirve como referencia única
> para implementar el sistema por hitos y para redactar la memoria del proyecto.
> En este documento solo se cubre la **detección por sonido**. El movimiento, el
> NavMesh y la máquina de estados completa del enemigo quedan fuera de alcance.
>
> **Este documento no fija valores numéricos.** Velocidades, radios, tiempos e
> aislamientos son campos serializados que se ajustan en el Inspector y cuyos valores por
> defecto viven en los propios scripts. Aquí se documenta qué controla cada parámetro y
> cómo se relacionan entre sí, para que se puedan cambiar sin tocar la documentación.

---

## Índice

1. Objetivo y alcance
2. Concepto de diseño
3. Glosario
4. Modelo de escucha acordado
5. Relación con los estados del Ciego
6. Arquitectura de scripts
7. Parámetros ajustables
8. Hitos de implementación
9. Plan de pruebas manual
10. Justificación técnica (para la memoria)
11. Decisiones pendientes
12. Fuera de alcance y trabajo futuro
13. Registro de cambios

---

## 1. Objetivo y alcance

El Ciego es un enemigo sin vista. Toda su información del entorno llega por el oído.
El objetivo de este subsistema es responder a una única pregunta:

> **¿Qué está oyendo el Ciego en este instante y en qué punto del mapa?**

A partir de esa respuesta, el futuro cerebro del enemigo decidirá si patrulla, sospecha,
persigue o rebusca. La detección no mueve al enemigo ni reproduce animaciones.

### Entra en este subsistema

- Radios de ruido del jugador (locomoción y respiración).
- Atenuación del sonido por paredes y obstáculos.
- Comprobación de audibilidad a intervalos regulares.
- Memoria de la última posición oída.
- Histéresis y tiempos de reacción para que la detección no parpadee.
- Información de salida que consumirá el futuro cerebro del Ciego.

### No entra en este subsistema (se hará después)

- Movimiento del Ciego (NavMesh, patrullas, persecución).
- Máquina de estados completa (Patrullar / Sospecha / Perseguir / Rebuscar).
- Animaciones y audio del enemigo.
- Mecánica de aguantar la respiración con tecla (aquí solo se deja preparada su entrada).
- Objetos ruidosos lanzables, puertas, radios o trampas.

---

## 2. Concepto de diseño

Pilares que justifican las decisiones de este documento:

1. **El Ciego no ve, pero no es tonto.** No conoce la posición del jugador: solo conoce
   los puntos del mapa donde ha oído algo. Esto hace posible el sigilo.
2. **El ruido no es una esfera permanente.** Solo existe mientras el jugador hace ruido.
   Un jugador quieto no emite pasos; solo respira.
3. **El sonido se atenúa con los obstáculos.** Una pared reduce el alcance del ruido.
   Varias paredes pueden hacerlo inaudible.
4. **El sistema debe ser barato y explicable.** Comprobaciones a intervalos regulares,
   distancias y raycasts. Sin física de audio, sin búsquedas de camino, sin estructuras
   complejas.
5. **Todo debe poder verse en el editor.** Radios y rayos dibujados con Gizmos para
   poder ajustar los valores a ojo.

### Decisión de arquitectura (resumen)

Se separan tres responsabilidades en tres scripts pequeños:

| Script | Dónde vive | Responsabilidad |
|---|---|---|
| `PlayerNoiseEmitter` | Jugador | Calcular cuánto ruido hace el jugador y dónde |
| `BlindHearing` | Ciego | Decidir si ese ruido le llega y con qué nivel |
| `HearingBlocker` | Paredes | Indicar cuánto aísla cada obstáculo |

La alternativa era meter todo dentro del Ciego. Se descarta porque el Ciego tendría que
conocer detalles internos del jugador (velocidad, stamina, respiración) y porque, cuando
lleguen los objetos ruidosos, habría que rehacerlo. Tres scripts con una responsabilidad
cada uno son más fáciles de explicar y de defender que un único script grande.

---

## 3. Glosario

- **Emisor:** punto del jugador desde el que nace el sonido (el pecho). No se emite
  desde los pies. Su altura se ajusta en el Inspector.
- **Oreja del Ciego:** punto donde escucha (cabeza). Todas las comprobaciones se hacen
  desde aquí.
- **Radio de locomoción:** alcance del ruido de las pisadas. Es la antigua "esfera exterior".
- **Radio de respiración:** alcance del ruido de respirar. Es la antigua "esfera interior".
- **Aislamiento:** metros que un obstáculo resta al radio del sonido que lo atraviesa.
- **Radio efectivo:** radio del ruido una vez descontados los obstáculos intermedios.
- **Última posición oída:** punto exacto del mapa donde el Ciego oyó ruido por última vez.
- **Histéresis:** usar un umbral distinto para entrar y para salir de un estado, de forma
  que no se active y desactive continuamente en el borde.
- **Nivel de escucha:** resultado del sistema en cada comprobación:
  `Nada`, `Sospecha` o `Persecución`.

---

## 4. Modelo de escucha acordado

### 4.1 Fuentes de ruido

Solo hay dos fuentes en esta fase:

| Fuente | Cuándo existe | Radio | Se bloquea con paredes |
|---|---|---|---|
| Locomoción (pisadas) | Mientras el jugador se mueve | Crece con la velocidad real, de 0 al radio de carrera | Sí, por atenuación |
| Respiración | Siempre, salvo aguantando la respiración | Crece con el cansancio, entre el radio base y el radio de fatiga | Sí (su radio es pequeño, un solo obstáculo suele bloquearla) |

Más adelante se podrán añadir fuentes nuevas (objetos lanzados, puertas, radios), pero
no forman parte de este sistema.

### 4.2 Radio de locomoción

**El radio se calcula con la velocidad horizontal real**, no con el nombre del estado.
Motivo: el sistema de pasos actual ya se basa en la distancia recorrida, así que un
jugador que empuja una pared no se mueve y no debe hacer ruido.

La tabla de radios tiene cuatro marcas y se interpola linealmente entre ellas:

| Situación | Velocidad real | Radio de locomoción |
|---|---|---|
| Parado | 0 | 0 (silencio) |
| Agachado andando | `CrouchWalkSpeed` | `crouchRadius` |
| Andando | `WalkSpeed` | `walkRadius` |
| Corriendo | `SprintSpeed` | `runRadius` |

- Por debajo de la velocidad de agachado se interpola de 0 al radio de agachado.
- Entre la velocidad de agachado y la de andar se interpola entre sus dos radios.
- Entre la velocidad de andar y la de correr se interpola entre sus dos radios.
- Por encima de la velocidad de correr se mantiene el radio de carrera.

Las tres velocidades de referencia se leen del `PlayerController`, así que la tabla cae
siempre sobre las velocidades reales del jugador y no hay que duplicar valores. Los tres
radios se ajustan en el Inspector.

Al correr, la velocidad sube gradualmente (mezcla de sprint del `PlayerController`), así
que el radio crece de forma continua: no hay saltos entre "andar" y "correr".

**Decaimiento al parar:** cuando la velocidad cae a cero, el radio no desaparece de golpe.
Baja de forma lineal hasta cero durante `noiseHoldTime`. Ese pequeño margen
representa el eco del último paso y evita que un solo frame sin velocidad corte la escucha.

### 4.3 Radio de respiración

La respiración existe siempre que el jugador no la esté aguantando. Su radio depende del
cansancio:

```
radioRespiracion = radioBase + radioFatiga * (1 - staminaNormalizada)
```

- Con la stamina llena: `baseBreathRadius`.
- Sin stamina (recién agotado): `baseBreathRadius + fatigueBreathRadius`.
- Aguantando la respiración: 0.

Ambos radios se ajustan en el Inspector; este documento no fija sus valores.

Esto conecta con el sistema de stamina ya existente: correr tiene un coste inmediato
(más ruido de pisadas) y otro posterior (respiras más fuerte durante la recuperación).
También da una razón de diseño para no correr sin necesidad.

En esta fase, "aguantar la respiración" no tiene tecla todavía: se expone como una
propiedad pública y un campo de prueba serializado para poder activarla desde el inspector
o desde código. La tecla, la duración máxima y el jadeo al soltar se implementarán en el
subsistema de jugador.

### 4.4 Oclusión por paredes

No se cuentan paredes. Se **resta aislamiento**:

```
radioEfectivo = radioDelRuido - sumaDeAislamientos
audible       = distancia(origenDelRuido, orejaDelCiego) <= radioEfectivo
```

Cada obstáculo que el sonido atraviesa aporta su valor de aislamiento. Así una pared fina
puede no bloquear y dos paredes sí, con un solo número ajustable por material.

**Ejemplo (con valores genéricos)**

- Jugador andando con radio `R`, Ciego a distancia `D` y sin obstáculos: `R - 0 >= D`,
  le oye.
- Mismo caso con un obstáculo de aislamiento `A`: `R - A`; si queda por debajo de `D`,
  no le oye. Si queda justo en `D`, le oye en el límite.
- Dos obstáculos con aislamientos `A1` y `A2`: se restan los dos, `R - A1 - A2`.

Los valores concretos de `R`, `D` y `A` salen del Inspector y del nivel, no de este
documento.

Para la respiración se usa la misma fórmula. Como su radio es pequeño, cualquier
obstáculo con un aislamiento igual o mayor que ese radio la bloquea por completo. Es
decir: **al otro lado de una pared no te oye respirar, en la misma sala sí**, siempre que
el aislamiento de la pared supere el radio de respiración.

Solo cuentan los objetos marcados con el componente `HearingBlocker` y en la capa
`HearingBlocker`. Mesas, alfombras, cajas y decorados no bloquean el sonido salvo que se
decida marcarlos a propósito. Esto evita que un objeto bajo tape un rayo que representa
sonido y que debería pasar por encima.

**Detalles de implementación**

- Un único rayo desde la oreja del Ciego hasta el emisor del jugador.
- `Physics.RaycastNonAlloc` con un buffer pequeño y una máscara de capa.
- Los impactos se deduplican por componente: un mismo muro hecho de varias piezas no
  suma varias veces si comparten el mismo `HearingBlocker`.
- `QueryTriggerInteraction.Ignore` para no contar triggers decorativos.
- El rayo no puede golpear al propio jugador: la máscara solo incluye `HearingBlocker`.

### 4.5 Última posición oída

Cuando el Ciego oye algo, guarda el punto del emisor en ese instante y la marca de tiempo.
No guarda ni consulta la `Transform` del jugador.

Consecuencias de diseño:

- Si te oye y te vas en silencio, el Ciego va al punto donde te oyó, no a donde estás.
- Sirve directamente para el estado **Rebuscar**.
- Impide que la IA "haga trampas" siguiendo al jugador a través de las paredes.

### 4.6 Tiempos e histéresis

Los umbrales geométricos puros producen parpadeos en el borde del radio y reacciones
robóticas. Se añaden cuatro parámetros de estabilidad:

| Parámetro | Función |
|---|---|
| `reactionDelay` | Retardo antes de reaccionar a un ruido de pisadas |
| `breathReactionDelay` | Retardo antes de reaccionar a la respiración |
| `suspicionHoldTime` | Tiempo que sigue en Sospecha tras dejar de oírte |
| `exitMargin` | Margen extra para dejar de oír (histéresis) |

Los cuatro se ajustan en el Inspector.

La histéresis se aplica así: para pasar a Sospecha hay que estar dentro del radio
efectivo, pero para dejar de estarlo hay que salir del radio efectivo multiplicado por
`1 + exitMargin`. Evita que el estado cambie varias veces por segundo cuando el jugador
camina justo en el límite.

### 4.7 Frecuencia de comprobación y coste

- La comprobación se hace cada `hearCheckInterval`, no en cada frame. El valor se ajusta
  en el Inspector.
- Primero se compara la distancia con el radio **sin atenuar**. Si no entra, no se lanza
  ningún rayo.
- Solo se lanzan rayos si el jugador está emitiendo ruido (locomoción o respiración).
- Coste: unas pocas comprobaciones por segundo y, como máximo, un raycast corto por
  comprobación. Es despreciable incluso con varios enemigos.

No se usa `Physics.OverlapSphere`: para comparar dos distancias no hace falta consultar
el motor de física.

### 4.8 Verticalidad

Se usa distancia 3D. Los suelos y techos deben tener `HearingBlocker` como cualquier
pared. Consecuencia: un jugador en el piso de arriba queda bloqueado por el forjado,
salvo que se le dé un aislamiento bajo. Es el comportamiento realista y además sencillo
de ajustar sin casos especiales.

---

## 5. Relación con los estados del Ciego

Esta sección describe **cómo se usará la información de escucha**, no cómo se implementa
el movimiento. La máquina de estados completa es el siguiente subsistema.

| Estado | Qué escucha le interesa | Qué hace |
|---|---|---|
| Patrullar | Nada | Recorre su ruta sin saber nada del jugador |
| Sospecha | Ruido de locomoción dentro del radio efectivo | Se dirige al último punto oído, algo más rápido que al patrullar |
| Perseguir | Respiración dentro del radio efectivo | Va directo al punto oído mientras lo siga oyendo |
| Rebuscar | Nada durante un tiempo | Busca alrededor del último punto oído |

Transiciones previstas:

- **Patrullar → Sospecha:** se oye un ruido de locomoción.
- **Sospecha → Perseguir:** se oye la respiración.
- **Perseguir → Rebuscar:** se deja de oír durante `loseTargetTime`.
- **Rebuscar → Patrullar:** pasa `searchTime` sin oír nada.
- **Cualquier estado → Perseguir:** se oye la respiración.

Reglas de diseño asociadas:

- El Ciego nunca recibe la posición actual del jugador, solo `LastHeardPosition`.
- En Sospecha no debe "pegarse" al jugador: va al punto, llega, y si no oye nada más,
  pasa a Rebuscar o vuelve a Patrullar.
- La respiración es la única fuente que permite persecución directa, y está limitada por
  paredes. Por eso aguantar la respiración será una mecánica central.

El sistema de escucha entrega esta información (nivel actual y última posición oída) y no
decide nada más. Así el cerebro del enemigo se puede programar y probar por separado.

---

## 6. Arquitectura de scripts

### 6.1 Esquema general

```
[Jugador]
  PlayerNoiseEmitter  --(radio locomoción, radio respiración, posición, ¿emite ruido?)-->
                                                                                        |
                                                                                        v
[El Ciego]                                                                     BlindHearing
  - comprueba distancia a intervalos                                                   |
  - lanza un rayo solo si el ruido podría llegar                                       |
  - resta aislamientos                                                                 |
                                                                                        v
[Paredes]  HearingBlocker (aislamiento en metros)  ----------------------------->  nivel de escucha
                                                                                  última posición oída
```

### 6.2 `PlayerNoiseEmitter`

**Ubicación:** `Assets/PlayerFolder/PlayerNoiseEmitter.cs`
**GameObject:** el del jugador (junto a `PlayerController` y `CharacterController`).

**Responsabilidad única:** decir cuánto ruido hace el jugador y desde dónde.

Referencias:

- `PlayerController` (para leer las velocidades de las marcas de la tabla y la stamina
  cuando llegue el hito de fatiga).
- `CharacterController` (para leer la velocidad horizontal real).
- `Transform` opcional del pecho; si está vacío se calcula con la altura configurada.

Ajustes (todos `[SerializeField]`):

| Campo | Tipo | Uso |
|---|---|---|
| `chestHeight` | float | Altura del punto de emisión |
| `noiseHoldTime` | float | Margen tras parar antes de silenciar el radio |
| `crouchRadius` | float | Radio de pisadas a la velocidad de agachado |
| `walkRadius` | float | Radio de pisadas a la velocidad de andar |
| `runRadius` | float | Radio de pisadas a la velocidad de correr |
| `baseBreathRadius` | float | Respiración con stamina llena |
| `fatigueBreathRadius` | float | Extra de radio al estar agotado |

Las velocidades de las marcas no son campos de este script: se leen del
`PlayerController` (`CrouchWalkSpeed`, `WalkSpeed`, `SprintSpeed`).

API pública (solo lectura, para que la lea el Ciego):

```csharp
public float LocomotionRadius { get; }   // radio de pisadas actual (0 si lleva parado un rato)
public float BreathRadius     { get; }   // radio de respiración actual (0 si aguanta el aire)
public bool  MakesNoise       { get; }   // true si LocomotionRadius > 0
public Vector3 EmitPosition   { get; }   // punto del pecho desde el que se emite
public bool  IsHoldingBreath  { get; set; } // preparado para la futura mecánica
```

Notas de implementación:

- La velocidad se lee del `CharacterController` y se le pone la componente Y a cero.
- La tabla de radios se resuelve con tres `Mathf.InverseLerp` / `Mathf.Lerp` seguidos
  (tres tramos), sin curvas ni animaciones.
- El decaimiento se guarda con un temporizador simple: si hay velocidad, se refresca el
  temporizador; si no, el radio baja proporcionalmente hasta cero al agotarse.
- La stamina se leerá con una propiedad nueva de solo lectura en `PlayerController`
  (`StaminaNormalized`), que se añadirá en el hito 4. En el hito 1 ya se añadieron
  `SprintSpeed` y `CrouchWalkSpeed` para no duplicar las velocidades en el emisor.
- `OnDrawGizmosSelected` dibuja los dos radios con colores distintos.

### 6.3 `BlindHearing`

**Ubicación:** `Assets/EnemyFolder/BlindHearing.cs`
**GameObject:** el del Ciego (o un hijo suyo).

**Responsabilidad única:** decidir si el ruido del jugador le llega y con qué nivel.

Referencias:

- `PlayerNoiseEmitter` (se busca automáticamente si no se asigna).
- `Transform` de la oreja; si está vacío se usa este GameObject más una altura de cabeza.

Ajustes principales:

| Campo | Tipo | Uso |
|---|---|---|
| `earHeight` | float | Altura de la oreja si no hay transform |
| `hearCheckInterval` | float | Frecuencia de comprobación (no cada frame) |
| `reactionDelay` | float | Reacción a pisadas |
| `breathReactionDelay` | float | Reacción a la respiración |
| `suspicionHoldTime` | float | Memoria tras dejar de oír pisadas |
| `exitMargin` | float | Histéresis al salir del radio |
| `obstacleMask` | LayerMask | Qué capas atenúan el sonido (por defecto, `HearingBlocker`) |
| `drawDebug` | bool | Dibuja rayos y radios en la escena |

Salida pública (solo lectura, la consumirá el futuro cerebro):

```csharp
public enum HearingLevel { Nothing, Suspicion, Chase }

public HearingLevel CurrentLevel { get; }      // nivel actual
public Vector3 LastHeardPosition { get; }      // último punto oído (no la posición del jugador)
public float TimeSinceLastHeard { get; }       // segundos desde el último ruido oído
public float RawLocomotionRadius { get; }      // radios sin atenuar, para Gizmos
public float RawBreathRadius { get; }
```

Lógica interna (resumida):

1. Cada `hearCheckInterval`, leer los radios y la posición del emisor.
2. Calcular distancias a la oreja.
3. Si la distancia supera el radio bruto, no hay nada que oír: no se lanza rayo.
4. Si entra, lanzar un rayo y sumar aislamientos (deduplicando componentes).
5. Calcular radio efectivo y decidir si es audible.
6. Aplicar histéresis y temporizadores.
7. Actualizar `CurrentLevel`, `LastHeardPosition` y `TimeSinceLastHeard`.

Prioridad de los niveles: si se oye respiración, `Chase`; si solo se oyen pisadas,
`Suspicion`; si no se oye nada, se mantiene el nivel anterior hasta que se agote el
tiempo de memoria y después pasa a `Nothing`.

No hay eventos ni delegados: el futuro cerebro leerá estas propiedades en su `Update`.
Es más sencillo de leer, de depurar y de explicar.

### 6.4 `HearingBlocker`

**Ubicación:** `Assets/EnemyFolder/HearingBlocker.cs`
**GameObject:** paredes, puertas, suelos, techos y cualquier obstáculo que deba atenuar.

```csharp
public class HearingBlocker : MonoBehaviour
{
    [SerializeField] private float insulation;
    public float Insulation { get { return insulation; } }
}
```

El aislamiento se ajusta por objeto en el Inspector. Como guía de nivel, no como tabla de
valores cerrados:

| Material | Aislamiento relativo |
|---|---|
| Pared de hormigón / ladrillo, forjado | Alto |
| Tabique fino | Medio |
| Puerta de madera cerrada | Bajo |
| Cristal / ventana cerrada | Muy bajo |

La regla que sí importa: un aislamiento igual o mayor que el radio del ruido lo bloquea
por completo, y varios obstáculos suman. Para que una puerta bloquee la respiración, su
aislamiento debe superar el radio de respiración; si se quiere que la deje pasar, tiene
que quedarse por debajo.

El componente no hace nada por sí mismo: solo aporta su valor cuando un rayo lo golpea.
Es intencionadamente tonto para que sea trivial de explicar.

### 6.5 Configuración de escena (una sola vez)

1. Crear la capa `HearingBlocker` en `Project Settings > Tags and Layers`.
2. Crear la capa `Player` y asignarla al jugador (opcional, útil para otras consultas).
3. Añadir `PlayerNoiseEmitter` al jugador.
4. Añadir `BlindHearing` al Ciego y crear un hijo vacío `HearingOrigin` a la altura de la
   cabeza (o dejar `earHeight` por defecto).
5. Añadir `HearingBlocker` a las paredes del nivel de pruebas y ajustar el aislamiento.
6. Asignar la máscara `HearingBlocker` en `BlindHearing`.

### 6.6 Qué NO se va a usar (y por qué)

- **Sin interfaces ni clases abstractas:** no aportan nada con dos fuentes de ruido.
- **Sin `ScriptableObject`:** los valores se ajustan en el inspector; no hace falta más.
- **Sin eventos ni bus de sonido global:** un solo enemigo y un solo jugador.
- **Sin `Physics.OverlapSphere`:** comparar distancias es más barato.
- **Sin NavMesh ni movimiento:** es el siguiente subsistema, no este.
- **Sin audio 3D real ni `AudioSource`:** el "sonido" es un número, no un clip.

---

## 7. Parámetros ajustables

Este documento **no fija valores**. Todos los parámetros son campos serializados que se
ajustan en el Inspector, y sus valores por defecto viven en los propios scripts. Aquí solo
se documenta qué controla cada uno, para poder revisarlos de un vistazo sin tener que
actualizar el documento cada vez que se ajusta el juego.

### `PlayerNoiseEmitter`

| Parámetro | Qué controla |
|---|---|
| `playerController` | De dónde se leen las velocidades de agachado, andar y correr |
| `chestHeight` | Altura del punto de emisión |
| `noiseHoldTime` | Cuánto tarda el radio de pisadas en bajar a 0 al parar |
| `crouchRadius` | Radio de pisadas a la velocidad de agachado |
| `walkRadius` | Radio de pisadas a la velocidad de andar |
| `runRadius` | Radio de pisadas a la velocidad de correr |
| `baseBreathRadius` | Radio de respiración con la stamina llena |
| `fatigueBreathRadius` | Extra de radio de respiración al estar agotado |

### `BlindHearing`

| Parámetro | Qué controla |
|---|---|
| `earHeight` | Altura de la oreja si no hay transform |
| `hearCheckInterval` | Cada cuánto se comprueba la escucha |
| `reactionDelay` | Retardo al reaccionar a las pisadas |
| `breathReactionDelay` | Retardo al reaccionar a la respiración |
| `suspicionHoldTime` | Cuánto se mantiene la Sospecha tras dejar de oírte |
| `exitMargin` | Margen de histéresis al salir del radio |
| `loseTargetTime` | Tiempo sin oír para pasar a Rebuscar (futuro estado Perseguir) |
| `searchTime` | Tiempo de búsqueda antes de volver a Patrullar (futuro estado Rebuscar) |

### `HearingBlocker`

| Parámetro | Qué controla |
|---|---|
| `insulation` | Metros que resta al radio del sonido que lo atraviesa |

> `loseTargetTime` y `searchTime` se documentan aquí porque forman parte del diseño de la
> escucha, pero su uso real corresponde al siguiente subsistema.

---

## 8. Hitos de implementación

Cada hito es jugable y verificable por separado. No se empieza uno hasta que el anterior
cumple su criterio de aceptación.

### Hito 0 — Preparación del escenario de pruebas

**Objetivo:** tener un lugar donde medir distancias y paredes.

**Se hace:**

- Crear las capas `Player` y `HearingBlocker`.
- Montar una escena de pruebas sencilla: una sala con el Ciego y el jugador, y una
  segunda sala separada por una pared con puerta.
- Colocar referencias de distancia visuales (marcas de distancia) para ajustar radios.

**Criterio de aceptación:**

- La escena entra en Play sin errores.
- Las dos capas aparecen en `Tags and Layers` y se pueden asignar.

**No incluye:** ningún script nuevo.

### Hito 1 — Emisor de ruido del jugador

**Objetivo:** representar numéricamente el ruido que hace el jugador.

**Se hace:**

- `PlayerNoiseEmitter` con velocidad real, tabla de radios, decaimiento y respiración
  base (sin fatiga todavía).
- `IsHoldingBreath` (propiedad pública más campo de prueba serializado, por defecto
  `false`) para poder forzarlo desde el inspector.
- Gizmos de los dos radios.

**Criterio de aceptación:**

- Parado: el radio de locomoción baja a 0 en `noiseHoldTime`.
- Andando: el radio se estabiliza en `walkRadius`.
- Corriendo: el radio sube de forma continua hasta `runRadius`.
- Agachado andando: el radio se estabiliza en `crouchRadius`.
- El radio de respiración se mantiene en `baseBreathRadius` y desaparece al marcar
  `IsHoldingBreath`.

**No incluye:** el Ciego, paredes, fatiga.

### Hito 2 — Escucha básica por distancia

**Objetivo:** que el Ciego reaccione al ruido sin tener en cuenta paredes.

**Se hace:**

- `BlindHearing` con comprobación cada `hearCheckInterval`.
- Distancia 3D entre la oreja y el emisor.
- Niveles `Nothing`, `Suspicion`, `Chase` y última posición oída.
- Gizmos del radio escuchado y del último punto oído.
- En este hito la reacción es inmediata y puede parpadear en el borde; es esperado.

**Criterio de aceptación:**

- Jugador parado sin emitir pisadas: nunca hay Sospecha (solo respiración, que en este
  hito se puede ignorar o tratar aparte).
- Jugador andando dentro del radio de andar: Sospecha.
- Jugador andando fuera del radio de andar: nada.
- Jugador corriendo dentro del radio de carrera: Sospecha.
- `LastHeardPosition` se actualiza solo cuando hay ruido y no sigue al jugador.

**No incluye:** paredes, tiempos de memoria, histéresis.

### Hito 3 — Oclusión por paredes

**Objetivo:** que las paredes atenúen y puedan bloquear el sonido.

**Se hace:**

- `HearingBlocker` con aislamiento configurable.
- Capa `HearingBlocker` y máscara en `BlindHearing`.
- Raycast desde la oreja al emisor con `RaycastNonAlloc`, deduplicando componentes.
- Resta de aislamientos y comparación con la distancia.
- Rayo visible con `drawDebug`.
- Marcar paredes, puertas y forjados del escenario de pruebas.

**Criterio de aceptación:**

- Andando con un obstáculo cuyo aislamiento iguala o supera el radio de andar: no hay
  Sospecha.
- Andando con un obstáculo cuyo aislamiento es menor que el radio de andar: sí hay
  Sospecha.
- Andando con dos obstáculos cuyos aislamientos suman más que el radio: no hay Sospecha.
- Un objeto sin `HearingBlocker` entre medias no bloquea.
- El rayo no golpea al jugador ni dispara errores de física.

### Hito 4 — Respiración, fatiga y aguantar la respiración

**Objetivo:** que la esfera interior sea una amenaza real y esté ligada al cansancio.

**Se hace:**

- Propiedad de solo lectura `StaminaNormalized` en `PlayerController`.
- Fórmula de respiración con fatiga en `PlayerNoiseEmitter`.
- Nivel `Chase` en `BlindHearing` cuando se oye la respiración.
- `IsHoldingBreath` controlable (por ahora desde el inspector; la tecla llegará después).
- Misma atenuación por paredes para la respiración.

**Criterio de aceptación:**

- Dentro del radio de respiración, sin paredes y sin aguantar el aire: `Chase`.
- Dentro del radio de respiración pero con un obstáculo cuyo aislamiento lo supera: no
  hay `Chase`.
- Aguantando la respiración (forzado desde el inspector): no hay `Chase` ni de cerca.
- Tras correr hasta agotar la stamina, el radio de respiración sube hasta
  `baseBreathRadius + fatigueBreathRadius` y vuelve a `baseBreathRadius` al recuperarla.
- La respiración no provoca `Suspicion`: solo `Chase` o nada.

**No incluye:** tecla de aguantar la respiración, límite de tiempo, jadeo al soltar.

### Hito 5 — Tiempos, histéresis y memoria

**Objetivo:** que la detección sea estable y creíble.

**Se hace:**

- `reactionDelay` y `breathReactionDelay`.
- `suspicionHoldTime` y `exitMargin`.
- `TimeSinceLastHeard`.
- Degradación de nivel: `Chase` → `Suspicion` → `Nothing` por tiempo sin oír.
- `lastHeardPosition` conservada hasta que se oiga algo nuevo o se complete la búsqueda.

**Criterio de aceptación:**

- Cruzar el borde del radio andando no produce cambios de nivel cada frame.
- Tras dejar de andar, el nivel se mantiene `suspicionHoldTime` y después baja.
- Si te alejas en silencio, `LastHeardPosition` no se mueve.
- La reacción a la respiración es claramente más rápida que a las pisadas.

**No incluye:** estados completos ni movimiento.

### Hito 6 — Integración, depuración y cierre

**Objetivo:** dejar el sistema listo para que lo consuma el cerebro del Ciego.

**Se hace:**

- Mensaje de depuración (consola o texto en pantalla) cuando cambia el nivel.
- Revisión final de los parámetros de la sección 7.
- Comprobación de que no hay errores ni avisos en consola.
- Actualización de este documento con capturas de los Gizmos (sin fijar valores).
- Repaso de la lista de pruebas de la sección 9.

**Criterio de aceptación:**

- Todas las pruebas de la sección 9 pasan.
- `CurrentLevel` y `LastHeardPosition` se pueden leer desde otro script sin errores.
- El sistema funciona con el Ciego quieto (el movimiento es otro subsistema).

**No incluye:** máquina de estados, NavMesh, animaciones, ataque.

---

## 9. Plan de pruebas manual

| Nº | Acción | Resultado esperado |
|---|---|---|
| 1 | Jugador parado dentro del radio de respiración, sin paredes | Sin Sospecha (solo respiración) |
| 2 | Andar dentro del radio de andar | Sospecha tras `reactionDelay` |
| 3 | Andar fuera del radio de andar | Nada |
| 4 | Correr dentro del radio de carrera | Sospecha |
| 5 | Agachado andando dentro del radio de agachado | Sospecha |
| 6 | Agachado andando fuera del radio de agachado | Nada |
| 7 | Andar con un obstáculo cuyo aislamiento supera el radio de andar | Nada |
| 8 | Andar con una puerta cuyo aislamiento no llega al radio de andar | Sospecha |
| 9 | Andar con dos obstáculos cuyos aislamientos suman más que el radio | Nada |
| 10 | Andar dentro del radio y parar | `suspicionHoldTime` de Sospecha y después Nada |
| 11 | Andar en el borde del radio | Sin parpadeos de nivel |
| 12 | Estar dentro del radio de respiración sin aguantar el aire | Persecución |
| 13 | Estar dentro del radio de respiración aguantando el aire | Nada |
| 14 | Estar dentro del radio de respiración tras una pared | Nada |
| 15 | Correr hasta agotar la stamina cerca del Ciego | Persecución (respiración más fuerte) |
| 16 | Hacer ruido y alejarse en silencio | El último punto oído no se mueve |
| 17 | Repetir 1-16 en el piso de arriba | Bloqueado por el forjado |

---

## 10. Justificación técnica (para la memoria)

### 10.1 Por qué no se usa un sistema de percepción existente

Unity no incluye un sistema de percepción equivalente al *AIPerception* de Unreal Engine.
Proporciona navegación (NavMesh) y física, pero la detección (vista, oído) se implementa
en el proyecto. Por tanto, este subsistema es necesario y no duplica funcionalidad del
motor.

### 10.2 Por qué radios y no audio real

El motor de audio no está pensado para consultar "¿me llega este sonido?". Reproducir
clips y consultar su estado en tiempo real sería más costoso, menos determinista y más
difícil de ajustar. Un modelo de radios con atenuación es:

- **Barato:** distancias y un raycast a intervalos regulares.
- **Determinista:** el mismo escenario produce siempre el mismo resultado, ideal para
  probar y para ajustar el diseño del nivel.
- **Legible:** los Gizmos muestran exactamente por qué el enemigo oye o no oye.

### 10.3 Por qué raycasts y no otra técnica

- **Contar paredes** es frágil: depende de cómo esté modelado el nivel y no distingue
  materiales.
- **`OverlapSphere`** devuelve todos los colliders del radio y obliga a filtrar: más
  coste y más código para el mismo resultado.
- **Raycast con atenuación** permite un número por material, ignora decorados y se
  explica en una frase: "cada pared resta metros al alcance del ruido".

### 10.4 Por qué separar emisor y receptor

El jugador es quien sabe cuánto ruido hace; el Ciego es quien decide si lo oye. Separarlo
permite:

- Probar el emisor con Gizmos sin colocar un enemigo.
- Reutilizar el emisor para futuros objetos ruidosos.
- Cambiar los valores del jugador sin tocar la IA.
- Explicar el sistema como dos responsabilidades independientes.

### 10.5 Coste

Con comprobaciones a intervalos y un máximo de un raycast corto por comprobación, el
coste por enemigo es despreciable en un escenario típico. No se consulta la física
cuando el jugador está fuera del radio o en silencio.

### 10.6 Limitaciones conocidas

- Un único rayo puede verse tapado por geometría baja; se mitiga con la capa
  `HearingBlocker` y, si hiciera falta, con varios rayos.
- No hay difracción del sonido (rodear esquinas): una pared bloquea aunque haya un hueco
  lateral.
- No hay propagación entre habitaciones conectadas por puertas abiertas; una puerta
  abierta debería tener aislamiento bajo o ninguno, y eso es una decisión de diseño del
  nivel.
- La respiración se modela como un radio constante con fatiga; no simula frecuencia
  respiratoria real.

---

## 11. Decisiones pendientes

Estas decisiones no bloquean los hitos 0-3, pero hay que cerrarlas antes de dar el
sistema por terminado:

1. **Agachado: ¿un radio pequeño o silencio total?**
   El `PlayerSoundController` actual no reproduce pasos agachado, así que hoy agacharse
   es sigilo perfecto. La propuesta de este documento es un radio pequeño (el de
   agachado) para que agacharse sea "muy silencioso" pero no invisible, y que el
   silencio total lo dé aguantar la respiración. Hay que confirmarlo.
2. **¿La respiración se bloquea por completo con cualquier pared?**
   Con los valores de partida, sí. Si se quiere permitir oírla a través de una puerta
   fina, basta con bajar su aislamiento por debajo del radio de respiración.
3. **Añadir `StaminaNormalized` a `PlayerController`.**
   Se hará en el hito 4, además de las propiedades `SprintSpeed` y `CrouchWalkSpeed`
   que ya se han añadido para leer las velocidades desde el emisor.
4. **Aguantar la respiración: tecla, duración y jadeo.**
   Pertenece al subsistema de jugador. Aquí solo se deja la propiedad preparada.
5. **¿Un rayo o varios?**
   Se empieza con uno. Si en pruebas resulta fácil "esconderse" detrás de objetos bajos,
   se añadirán dos rayos más (pies y cabeza) y se tomará el menos bloqueado.
6. **Puertas abiertas y cerradas.**
   En esta fase el aislamiento es fijo. Si una puerta cambia de estado, más adelante se
   ajustará su `insulation` desde código (por ejemplo, 0 si está abierta).
7. **Distancia 3D y forjados.**
   Se asume que los forjados llevan `HearingBlocker`. Si el nivel tiene muchas alturas y
   se quiere un comportamiento distinto, habría que añadir un límite vertical.

---

## 12. Fuera de alcance y trabajo futuro

Este documento termina cuando el Ciego sabe qué oye y dónde. Los siguientes subsistemas
son:

- **Cerebro del Ciego:** máquina de estados Patrullar / Sospecha / Perseguir / Rebuscar
  que lea `CurrentLevel` y `LastHeardPosition`.
- **Movimiento:** NavMesh (habrá que añadir el paquete *AI Navigation*), velocidades por
  estado y puntos de patrulla.
- **Aguantar la respiración:** tecla, barra de aire, límite y jadeo al soltar.
- **Objetos ruidosos:** piedras, botellas, puertas, radios. El diseño de emisor/receptor
  ya lo permite.
- **Varios enemigos:** si hay más de uno, evaluar un gestor central de ruido para no
  repetir comprobaciones.
- **Animaciones y audio del enemigo:** pasos, respiración, gruñidos de sospecha.
- **Dificultad:** multiplicadores de radio y de tiempos por nivel de dificultad.

---

## 13. Registro de cambios

| Versión | Fecha | Cambios |
|---|---|---|
| 1.0 | 15/11/2026 | Diseño inicial acordado: radios, atenuación por paredes, tiempos, arquitectura de tres scripts y siete hitos |
| 1.1 | 15/11/2026 | Los valores numéricos dejan de fijarse en el documento: velocidades, radios, tiempos y aislamientos se ajustan en el Inspector. La tabla de radios toma sus velocidades de `PlayerController` y los hitos y pruebas se describen de forma cualitativa |
