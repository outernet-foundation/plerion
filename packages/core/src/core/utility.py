from typing import Callable, cast

from cv2.typing import MatLike
from numpy import float16, float32
from numpy.typing import NDArray
from torch import Tensor, from_numpy  # type: ignore

tensor_from_numpy: Callable[[MatLike], Tensor] = cast(Callable[[MatLike], Tensor], from_numpy)


def to_f32(t: Tensor) -> NDArray[float32]:
    return t.detach().cpu().numpy().astype(float32, copy=False)


def to_f16(t: Tensor) -> NDArray[float16]:
    return t.detach().cpu().numpy().astype(float16, copy=False)
