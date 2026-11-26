# KOMIKAN (Unity 6, 2D)

Juego de estrategia asimétrico del pueblo Mapuche, implementado en Unity 6 en
2D con formas y colores simples generados por código.

## 1. Qué es KOMIKAN

KOMIKAN es un juego de tablero asimétrico: un bando pequeño y poderoso contra un
bando numeroso pero débil.

- **Pangui** (el puma) es una sola pieza. La controla la persona que juega. El
  Pangui es el único que puede capturar: devora a los perros saltando sobre
  ellos al estilo de las damas.
- **Trewas** (los perros) son 12 piezas. Las controla la inteligencia
  artificial (IA). Las Trewas no capturan: solo se mueven e intentan rodear al
  Pangui hasta dejarlo sin salida.

El objetivo de cada bando es opuesto: el Pangui quiere comer perros, y las
Trewas quieren acorralar al puma.

## 2. Cómo abrir el proyecto en Unity 6

1. Instala **Unity Hub**.
2. Desde Unity Hub, instala **Unity 6000.0.x** (el proyecto está fijado a la
   versión `6000.0.23f1` en `ProjectSettings/ProjectVersion.txt`).
3. En Unity Hub pulsa **Add** (Añadir) y selecciona la carpeta raíz del
   proyecto (`/projects/sandbox`, la carpeta que contiene `Assets/`,
   `Packages/` y `ProjectSettings/`). No abras una subcarpeta: abre la carpeta
   del proyecto completa.
4. Al abrir, Unity puede ofrecer **actualizar la versión del editor** si la que
   tienes instalada no es exactamente `6000.0.23f1`. Puedes aceptar la
   actualización a una versión `6000.0.x` sin problema; el proyecto está pensado
   para Unity 6.
5. Dentro del editor, abre la escena **`Assets/Scenes/Game.unity`**.
6. Pulsa **Play** para empezar a jugar.

La primera vez que se abre, Unity genera carpetas locales (`Library/`, `Temp/`,
`obj/`) y archivos de proyecto de C# (`.csproj`, `.sln`). Esos archivos no
forman parte del repositorio y se regeneran solos.

## 3. Cómo jugar

Tú controlas al **Pangui** (el puma). La **IA** mueve a las **Trewas** (los
perros). Los turnos se alternan: primero mueves tú, luego responde la IA tras
una breve pausa.

Controles del Pangui (con el ratón):

1. **Haz clic en el Pangui** para seleccionarlo. Al seleccionarlo se resaltan
   todos sus destinos legales: los puntos vecinos a los que puede dar un paso y
   los aterrizajes de captura disponibles.
2. **Movimiento simple:** haz clic en un punto destino resaltado vecino para
   mover el Pangui un paso por una línea del tablero hasta un punto vacío. Eso
   termina tu turno.
3. **Captura por salto (estilo damas):** si una Trewa está en un punto contiguo
   y justo detrás de ella (en la misma línea recta) hay un punto vacío, puedes
   saltar sobre la Trewa para aterrizar en ese punto vacío. La Trewa saltada es
   devorada y desaparece del tablero.
4. **Saltos encadenados:** después de un salto, si desde el nuevo punto hay otra
   captura disponible, el turno sigue activo y se vuelven a resaltar los
   aterrizajes posibles. Puedes encadenar varios saltos en un mismo turno,
   **incluso cambiando de dirección** entre un salto y el siguiente. El turno
   termina cuando ya no quedan saltos posibles o eliges detenerte al no haber
   continuaciones.

Hacer clic fuera de los destinos resaltados (cuando no estás en medio de una
cadena de saltos) deselecciona al Pangui.

## 4. Condiciones de victoria

- **El Pangui gana** cuando devora **6 Trewas**. En cuanto come al sexto perro,
  gana la partida (puede ocurrir incluso en mitad de una cadena de saltos).
- **Las Trewas ganan** cuando **acorralan al Pangui**: si al llegar el turno del
  Pangui este no tiene **ningún movimiento legal** (ni un paso simple ni una
  captura), las Trewas ganan.

## 5. Modelo del tablero (grafo)

El tablero se modela como un grafo explícito: una rejilla **alquerque de 5x5**
con **25 nodos**.

### Coordenadas

- Cada nodo tiene una coordenada `(col, fila)` con `col` entre 0 y 4 y `fila`
  entre 0 y 4.
- **Fila 0 = valle** (abajo): es la zona de inicio de las Trewas.
- **Fila 4 = montaña** (arriba): es la zona de inicio del Pangui.

### Adyacencia

- **Ortogonal en todos los nodos:** cada nodo se conecta con sus vecinos
  arriba, abajo, izquierda y derecha (dentro del tablero).
- **Diagonal solo donde `(col + fila)` es par:** desde esos nodos existen
  además las cuatro conexiones diagonales. Es el patrón diagonal clásico del
  alquerque. Desde los nodos con `(col + fila)` impar no hay diagonales.

Las piezas (tanto el Pangui como las Trewas) solo se mueven un paso a lo largo
de una conexión existente del grafo, hacia un punto vacío.

### Posiciones iniciales

- **Pangui:** un punto en `(2, 4)` (centro de la fila de la montaña, arriba).
- **Trewas (12 piezas):**
  - Toda la **fila 0**: `(0,0)`, `(1,0)`, `(2,0)`, `(3,0)`, `(4,0)` (5 perros).
  - Toda la **fila 1**: `(0,1)`, `(1,1)`, `(2,1)`, `(3,1)`, `(4,1)` (5 perros).
  - Dos perros más en la fila 2: `(1, 2)` y `(3, 2)`.

Suman 5 + 5 + 2 = 12 Trewas.

### Captura por salto y encadenado

Un salto de captura va en línea recta: desde el nodo del Pangui, en una
dirección, la Trewa saltada ocupa el nodo intermedio `(origen + paso)` y el
aterrizaje es el nodo `(origen + 2 x paso)`. El salto solo es válido si ambos
tramos de esa línea son conexiones reales del grafo (es decir, el salto recorre
una única línea del alquerque: ortogonal, o diagonal solo donde esas conexiones
existen), si hay una Trewa en el nodo intermedio y si el aterrizaje está vacío.

Tras aplicar un salto, la Trewa saltada se retira antes de calcular el siguiente
salto, por lo que en una misma cadena no se puede saltar dos veces sobre el
mismo perro. La cadena puede cambiar de dirección entre saltos.

## 6. La IA es fácil

La IA de las Trewas (`Assets/Scripts/TrewasAI.cs`) es **fácil y vencible a
propósito**. No usa minimax ni búsqueda profunda ni planificación de varios
pasos. Su heurística simple es:

1. Reúne todos los movimientos simples legales de todas las Trewas.
2. Puntúa cada movimiento candidato con:
   - un **valor aleatorio base** (entre 0 y 1) para que el juego no sea
     siempre óptimo y varíe de una partida a otra;
   - un **pequeño bonus** (+0,6) si el movimiento acerca a esa Trewa al Pangui
     según la distancia Manhattan, para empujar a la manada a rodearlo;
   - una **penalización ligera** (-0,8) que se aplica **solo a veces** (con una
     probabilidad del 50 %) cuando el destino dejaría a esa Trewa expuesta a una
     captura inmediata del Pangui. Es una comprobación superficial de un solo
     turno, no una predicción profunda, así que la IA sigue cometiendo errores.
3. Elige el candidato con mayor puntuación.

Como esa comprobación de seguridad solo se aplica la mitad de las veces, la IA
deja huecos de forma habitual y la persona puede ganar.

## 7. Nota de desarrollo

Este proyecto se creó **sin ejecutar el editor de Unity** (no había un runtime de
Unity disponible durante el desarrollo). El código C# se escribió para que
compile de forma segura por revisión manual, pero **no se ha ejecutado**. Para
verificarlo, abre el proyecto en Unity 6 y pulsa **Play** como se indica arriba.

Los gráficos son **formas y colores simples generados por código**: no hay
assets de arte binarios. Cada pieza y cada punto del tablero es un círculo
dibujado en tiempo de ejecución sobre una textura (`Assets/Scripts/SpriteFactory.cs`).
Los colores reales son:

- **Pangui:** naranja de puma (RGB aproximado 0.90, 0.35, 0.12).
- **Trewas:** azul (RGB aproximado 0.20, 0.45, 0.85).
- **Marcadores de los puntos del tablero:** gris claro, para mostrar los puntos
  donde se puede hacer clic.
