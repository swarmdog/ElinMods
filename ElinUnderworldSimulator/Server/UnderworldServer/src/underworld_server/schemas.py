from __future__ import annotations

from pydantic import BaseModel, Field


class RegisterRequest(BaseModel):
    install_key: str = Field(min_length=16, max_length=128)
    display_name: str = Field(min_length=1, max_length=80)
    game_version: str | None = Field(default=None, max_length=40)
    mod_version: str | None = Field(default=None, max_length=40)


class RegisterResponse(BaseModel):
    player_id: int
    auth_token: str
    display_name: str
    underworld_rank: int


class LoginRequest(BaseModel):
    auth_token: str = Field(min_length=32, max_length=256)


class LoginResponse(BaseModel):
    player_id: int
    display_name: str
    underworld_rank: int
    total_rep: int
    faction_id: int | None = None


class OrderAcceptRequest(BaseModel):
    order_id: int


class OrderAcceptResponse(BaseModel):
    status: str
    order_id: int
    deadline: str


class OrderDto(BaseModel):
    id: int
    territory_id: str
    territory_name: str
    client_type: str
    client_name: str
    product_type: str
    product_id: str | None = None
    min_quantity: int
    max_quantity: int
    min_potency: int
    max_toxicity: int
    base_payout: int
    deadline_hours: int
    created_at: str
    expires_at: str


class OrderListResponse(BaseModel):
    orders: list[OrderDto] = Field(default_factory=list)


class ShipmentSubmitRequest(BaseModel):
    order_id: int
    quantity: int = Field(gt=0)
    avg_potency: int = Field(ge=0)
    avg_toxicity: int = Field(ge=0)
    avg_traceability: int = Field(ge=0)
    item_ids: list[str] = Field(default_factory=list)


class EnforcementEvent(BaseModel):
    type: str
    confiscated_quantity: int | None = None
    gold_penalty: int | None = None
    investigation_days: int | None = None
    message: str


class ShipmentSubmitResponse(BaseModel):
    shipment_id: int
    outcome: str
    satisfaction_score: float
    final_payout: int
    heat_delta: int
    rep_delta: int
    enforcement_event: EnforcementEvent | None = None
    territory_heat_after: int


class ShipmentResultDto(BaseModel):
    shipment_id: int
    order_id: int
    outcome: str
    final_payout: int
    rep_delta: int
    enforcement_event: EnforcementEvent | None = None


class TerritoryChangeDto(BaseModel):
    territory_id: str
    old_faction: str | None = None
    new_faction: str | None = None
    message: str


class RankChangeDto(BaseModel):
    old_rank: int
    new_rank: int
    old_rank_name: str
    new_rank_name: str
    message: str


class ShipmentResultsResponse(BaseModel):
    results: list[ShipmentResultDto] = Field(default_factory=list)
    territory_changes: list[TerritoryChangeDto] = Field(default_factory=list)
    rank_change: RankChangeDto | None = None


class TerritoryDto(BaseModel):
    id: str
    name: str
    heat: int
    heat_level: str
    controlling_faction: str | None = None
    controlling_faction_id: int | None = None
    available_orders_count: int


class TerritoriesResponse(BaseModel):
    territories: list[TerritoryDto] = Field(default_factory=list)


class FactionCreateRequest(BaseModel):
    name: str = Field(min_length=3, max_length=24)


class FactionCreateResponse(BaseModel):
    faction_id: int
    name: str


class FactionJoinRequest(BaseModel):
    faction_id: int


class FactionJoinResponse(BaseModel):
    status: str
    faction_name: str


class FactionLeaveResponse(BaseModel):
    status: str
    faction_name: str


class FactionDisbandResponse(BaseModel):
    status: str
    faction_name: str


class FactionPromoteRequest(BaseModel):
    player_id: int


class FactionPromoteResponse(BaseModel):
    status: str
    player_display_name: str
    new_role: str


class FactionMemberDto(BaseModel):
    display_name: str
    role: str
    rank: int


class FactionDetailResponse(BaseModel):
    id: int
    name: str
    leader: str
    member_count: int
    max_members: int
    controlled_territories: list[str] = Field(default_factory=list)
    members: list[FactionMemberDto] = Field(default_factory=list)


class PlayerFactionDto(BaseModel):
    id: int
    name: str
    role: str


class PlayerStatusResponse(BaseModel):
    player_id: int
    display_name: str
    underworld_rank: int
    rank_name: str
    total_rep: int
    gold: int
    faction: PlayerFactionDto | None = None
    reputation_by_territory: dict[str, int] = Field(default_factory=dict)
    active_orders_count: int
    pending_shipments_count: int
