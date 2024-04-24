CREATE TABLE public.$safeitemname$ (
    id UUID NOT NULL,
    PRIMARY KEY(id)
);


CREATE INDEX $safeitemname$_id_idx ON public.$safeitemname$ (id);