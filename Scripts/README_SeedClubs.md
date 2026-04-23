# UK Tennis Clubs Seed Data

## Files

| File | Description | Clubs |
|------|-------------|-------|
| `SeedAllClubs.sql` | Master script (SQLCMD mode - uses `:r` to include parts) | 1,268 |
| `SeedClubs_Part1_East.sql` | Norfolk, Bedfordshire, Suffolk, Cambridgeshire | 158 |
| `SeedClubs_Part2_Midlands.sql` | Derbyshire, Lincolnshire, Shropshire, Leicestershire, Northants, Notts, Warwickshire, Staffs, H&W | 243 |
| `SeedClubs_Part3_North.sql` | Cumbria, Durham & Cleveland, Northumberland, Isle of Man, Yorkshire | 242 |
| `SeedClubs_Part4_SouthWest.sql` | Channel Islands, Dorset, Wiltshire, Somerset, Avon, Gloucestershire, Devon, Cornwall, Hampshire & IoW | 319 |
| `SeedClubs_Part5_Remaining.sql` | Hertfordshire, Essex, Berkshire, Buckinghamshire, Oxfordshire, Scotland, Wales | 306 |
| `SeedClubPlayers.sql` | Seeds 50 players into a specific club | 50 players |

## How to Run

### Option 1: SQLCMD Mode (SSMS)
1. Open SSMS
2. Enable SQLCMD Mode: Query menu > SQLCMD Mode
3. Open `SeedAllClubs.sql`
4. Execute

### Option 2: Run Parts Individually
Run each `SeedClubs_PartN_*.sql` file individually in order.

### Option 3: Concatenate
```powershell
Get-Content SeedClubs_Part1_East.sql, SeedClubs_Part2_Midlands.sql, SeedClubs_Part3_North.sql, SeedClubs_Part4_SouthWest.sql, SeedClubs_Part5_Remaining.sql | Set-Content SeedClubs_All.sql
```

## Data Quality Notes

- **Full addresses**: ~450 clubs have complete address data (AddressLine1, Town, Postcode)
- **Partial data**: ~820 clubs have Name and Town only (AddressLine1 is NULL)
- **Sources**: LTA ClubSpark county directories, club websites, tenniscourtsmap.com
- **Missing regions**: London & South East (Surrey, Sussex, Kent, Middlesex) - ~300 clubs not yet included
- Addresses can be enriched later via the LTA venue finder at lta.org.uk/play/find-a-tennis-court/

## Coverage

| Region | Counties | Clubs |
|--------|----------|-------|
| East England | Norfolk, Beds, Suffolk, Cambs | 158 |
| Midlands | Derbys, Lincs, Shrops, Leics, Northants, Notts, Warks, Staffs, H&W | 243 |
| North England | Cumbria, Durham, Northumberland, IoM, Yorkshire | 242 |
| South & SW | Channel Is, Dorset, Wilts, Somerset, Avon, Glos, Devon, Cornwall, Hants | 319 |
| South East | Herts, Essex, Berks, Bucks, Oxon | 206 |
| Scotland | All regions | 65 |
| Wales | All regions | 35 |
| **Total** | | **1,268** |
