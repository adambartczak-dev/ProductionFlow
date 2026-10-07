# ProductionFlow — Dzień 1: zakres i scenariusze

Dokument roboczy do nauki i implementacji. Zgodny z planem 50 etapów z 6 października 2026 r. Doprecyzowania reguł w tym dokumencie stanowią punkt startowy wersji 1, a nie opis istniejącego kodu.

## 1. Cel aplikacji

ProductionFlow to samodzielna aplikacja webowa do obsługi zleceń produkcyjnych. Planista przygotowuje i wydaje zlecenie, operator realizuje je na przydzielonym stanowisku, a system zapisuje wykonanie oraz historię zmian.

Projekt rozwija portfolio w kierunku Industrial Software / .NET. Wykorzystuje tematykę produkcji, ale działa na komputerze z danymi demonstracyjnymi. FactoryMonitoring pozostaje osobnym projektem.

## 2. Role

| Rola | Odpowiedzialność |
| --- | --- |
| Admin | Zarządza produktami, stanowiskami, kontami, rolami i przydziałami operatorów do stanowisk. |
| Planner | Tworzy zlecenia, edytuje szkice, wybiera stanowisko, wydaje i anuluje zlecenia zgodnie z regułami; przegląda postęp, historię i raporty. |
| Operator | Przegląda wydane zlecenia przydzielonych mu stanowisk, rozpoczyna realizację, dodaje raporty wykonania i kończy rozliczone zlecenie. |

Uprawnienia będą sprawdzane w API. Ukrycie przycisku w interfejsie nie stanowi zabezpieczenia. Uprawnienia Admina do operacji planisty lub operatora nie wynikają automatycznie z nazwy roli; ewentualne dodatkowe role trzeba nadać jawnie.

## 3. Pojęcia biznesowe

| Pojęcie | Znaczenie i podstawowe dane |
| --- | --- |
| Product | Produkt: identyfikator, unikalny kod i nazwa. |
| WorkCenter | Stanowisko produkcyjne: identyfikator, unikalny kod i nazwa. Nie musi oznaczać pojedynczej fizycznej maszyny. |
| ProductionOrder | Zlecenie: identyfikator, unikalny numer, produkt, stanowisko, planowana ilość, termin i status. |
| ExecutionReport | Przyrostowy raport wykonania: zlecenie, autor, czas, dobre sztuki i braki. |
| Historia zlecenia | Ślad zmian: kto, kiedy i co zmienił. |

„Przyrostowy” oznacza ilość wykonaną od poprzedniego wpisu, a nie stan licznika narastająco. Raport 60 sztuk, a później 40 sztuk daje łącznie 100.

## 4. Osiem historyjek użytkownika

Historyjka opisuje potrzebę w formie: „Jako [rola] chcę [czynność], aby [cel]”. Kryterium akceptacji określa zachowanie, które można sprawdzić.

| Nr | Historyjka | Kryterium akceptacji |
| --- | --- | --- |
| US-01 | Jako Admin chcę zarządzać produktami i stanowiskami, aby planista korzystał z dostępnego katalogu. | Poprawny wpis zostaje zapisany; pusty lub powtórzony kod jest odrzucany. |
| US-02 | Jako Admin chcę nadawać role i przydzielać operatorów do stanowisk, aby użytkownicy mieli odpowiedni dostęp. | Operator nie może obsłużyć zlecenia stanowiska, do którego nie jest przydzielony, również przez bezpośrednie żądanie do API. |
| US-03 | Jako Planner chcę utworzyć i edytować szkic zlecenia, aby przygotować produkcję. | Zlecenie ma status Draft, unikalny numer, istniejący produkt i stanowisko, dodatnią całkowitą ilość oraz termin. |
| US-04 | Jako Planner chcę wydać lub anulować zlecenie, aby zdecydować, czy ma trafić do realizacji. | Draft można wydać; Draft i Released można anulować. Niedozwolone przejście jest odrzucane bez zmiany danych. |
| US-05 | Jako Operator chcę rozpocząć wydane zlecenie, aby oznaczyć rozpoczęcie pracy. | Released przechodzi do InProgress na przydzielonym stanowisku; Draft nie można rozpocząć. |
| US-06 | Jako Operator chcę dopisywać dobre sztuki i braki oraz zakończyć rozliczone zlecenie, aby zapisać wynik produkcji. | Raporty nie przekraczają planowanej ilości. Zakończenie jest możliwe dopiero po rozliczeniu całego planu. |
| US-07 | Jako Planner chcę filtrować listę i przeglądać szczegóły zlecenia, aby znaleźć potrzebne dane i śledzić postęp. | Lista obsługuje status, wyszukiwanie, sortowanie i strony; szczegóły pokazują ilości, postęp i historię. Docelowo zmiany pojawiają się na żywo. |
| US-08 | Jako Planner chcę zobaczyć podsumowania zakończonych zleceń i pobrać CSV, aby analizować wyniki. | Zakończenie zlecenia uruchamia przetwarzanie w tle. Powtórzenie wiadomości nie nalicza wyniku drugi raz; raport sygnalizuje oczekiwanie na aktualizację. |

## 5. Statusy i dozwolone przejścia

| Status | Znaczenie | Dozwolona następna operacja |
| --- | --- | --- |
| Draft | Szkic przygotowywany przez planistę. | Planner: edycja, wydanie do Released lub anulowanie do Cancelled. |
| Released | Zlecenie wydane do realizacji. | Operator przydzielonego stanowiska: start do InProgress. Planner: anulowanie do Cancelled. |
| InProgress | Produkcja trwa. | Operator przydzielonego stanowiska: raportowanie i zakończenie do Completed po rozliczeniu planu. |
| Completed | Produkcja zakończona i ilość rozliczona. | Odczyt; brak kolejnych przejść w wersji 1. |
| Cancelled | Zlecenie anulowane przed rozpoczęciem produkcji. | Odczyt; brak kolejnych przejść w wersji 1. |

Doprecyzowanie na Dzień 1: anulowanie dopuszczamy tylko przed rozpoczęciem produkcji. Anulowanie częściowo wykonanego zlecenia wymagałoby dodatkowych reguł rozliczenia; pozostaje poza wersją 1.

Podstawowe dane zlecenia edytujemy tylko w Draft. Dodanie ostatniego raportu nie kończy zlecenia automatycznie: operator wykonuje oddzielną operację zakończenia. Próba edycji, raportowania lub zmiany statusu zakończonego albo anulowanego zlecenia jest odrzucana.

## 6. Reguły ilości i postępu

1. Planowana ilość jest dodatnią liczbą całkowitą.
2. Dobre sztuki i braki w raporcie są nieujemnymi liczbami całkowitymi. Raport musi zawierać łącznie co najmniej jedną sztukę.
3. Raport można dodać tylko do zlecenia InProgress na stanowisku przydzielonym operatorowi.
4. Suma dobrych sztuk i braków ze wszystkich raportów nie może przekroczyć planu.
5. Zakończenie wymaga statusu InProgress i sumy dobrych sztuk oraz braków równej planowanej ilości.
6. Postęp = (dobre sztuki + braki) / planowana ilość × 100%.
7. Błędny raport jest odrzucany w całości; nie zapisujemy go częściowo.

Plan oznacza tutaj łączną liczbę sztuk do rozliczenia. Dla planu 100 sztuk wynik 95 dobrych i 5 braków pozwala zakończyć zlecenie i daje 100% postępu. Nie oznacza to 100% dobrych sztuk. Jest to uproszczenie projektu, nie uniwersalna reguła MES.

## 7. Pełny scenariusz demonstracyjny

Dane: produkt P-001 „Obudowa”, stanowisko WC-01 „Montaż”, operator przydzielony do WC-01.

| Krok | Użytkownik i działanie | Oczekiwany wynik |
| --- | --- | --- |
| 1 | Admin przygotowuje katalog, role i przydział operatora. | Planista może wybrać produkt i stanowisko. |
| 2 | Planner tworzy PF-0001: produkt P-001, stanowisko WC-01, plan 100 sztuk i termin. | Draft; wykonanie 0; postęp 0%. |
| 3 | Planner wydaje zlecenie. | Released; historia zapisuje zmianę. |
| 4 | Operator rozpoczyna realizację. | InProgress; historia zapisuje zmianę. |
| 5 | Operator dodaje raport: 57 dobrych i 3 braki. | Łącznie 60 rozliczonych sztuk; postęp 60%. |
| 6 | Operator próbuje zakończyć zlecenie. | Odmowa; pozostaje InProgress, bo brakuje 40 sztuk. |
| 7 | Operator dodaje raport: 38 dobrych i 2 braki. | Łącznie 95 dobrych i 5 braków; postęp 100%; nadal InProgress. |
| 8 | Operator kończy zlecenie. | Completed; historia zapisuje zmianę. Docelowo powstaje zdarzenie OrderCompleted. |
| 9 | Planner przegląda wynik i raport. | Widzi 95 dobrych, 5 braków i historię; docelowo worker uzupełnia podsumowanie, dostępne także jako CSV. |

Scenariusze błędne do późniejszych testów:
- Start zlecenia Draft — odmowa.
- Raport operatora z nieprzydzielonego stanowiska — odmowa.
- Raport z ujemną ilością — odmowa.
- Dodatkowy raport 41 sztuk po rozliczeniu 60 z planowanych 100 — odmowa całego raportu; pozostaje 60.
- Anulowanie InProgress — odmowa według reguły wersji 1.
- Ponowne uruchomienie Completed — odmowa.
- Edycja przy użyciu nieaktualnej wersji danych — docelowo jawny konflikt zamiast nadpisania cudzej zmiany.

## 8. Granice wersji 1

Budujemy katalog, zlecenia, raporty wykonania, historię, role, interfejs webowy, aktualizacje na żywo, podsumowania w tle, CSV, testy, CI i uruchomienie przez Docker Compose.

Poza zakresem: pełny MES/ERP, magazyn, planowanie wielozmianowe, OEE i wielodostępny SaaS. Integracja PLC/OPC UA/MQTT z FactoryMonitoring jest ewentualnym późniejszym dodatkiem. Podstawowy plan działa lokalnie i nie wymaga płatnej chmury.

## 9. Zadanie na zakończenie Dnia 1

Przeczytaj dokument i odpowiedz własnymi słowami:

1. Czym Product różni się od ProductionOrder?
2. Co odróżnia Released od InProgress?
3. Dlaczego plan 100 sztuk można rozliczyć jako 95 dobrych i 5 braków?
4. Dlaczego ukrycie przycisku w React nie wystarcza do kontroli uprawnień?
5. Po wykonaniu 60 z planowanych 100 sztuk wpisujesz raport 41 sztuk. Co powinien zrobić system i jaki wynik ma pozostać?

Dzień 1 kończymy, gdy rozumiesz role, przebieg PF-0001, reguły zakończenia oraz granice wersji 1. Dokument w Dniu 2 umieścimy w repozytorium jako docs/requirements.md.
